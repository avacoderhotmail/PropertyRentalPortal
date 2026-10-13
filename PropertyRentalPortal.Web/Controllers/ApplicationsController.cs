using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PropertyRentalPortal.Web.Data.Entities;
using PropertyRentalPortal.Web.Data.Enums;
using PropertyRentalPortal.Web.Data.Seeding;
using PropertyRentalPortal.Web.Models;
using PropertyRentalPortal.Web.Services;

namespace PropertyRentalPortal.Web.Controllers;

[Authorize]
public class ApplicationsController : Controller
{
    private readonly IApplicationService _applicationService;
    private readonly IUnitService _unitService;

    public ApplicationsController(
        IApplicationService applicationService,
        IUnitService unitService)
    {
        _applicationService = applicationService;
        _unitService = unitService;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private bool IsPropertyManager => User.IsInRole(DatabaseSeeder.PropertyManagerRole);

    // ==========================================
    // 1. APPLICATION LIST (Filtered in Database)
    // ==========================================

    public async Task<IActionResult> Index(ApplicationListFilterViewModel filter)
    {
        filter.Page = Math.Max(1, filter.Page);
        filter.PageSize = filter.PageSize <= 0 ? 10 : filter.PageSize;

        var applications = await _applicationService.GetApplicationsListAsync(
            CurrentUserId,
            IsPropertyManager,
            filter.Status,
            filter.PropertyId,
            filter.SortBy,
            filter.SortDescending,
            filter.Page,
            filter.PageSize);

        var totalCount = await _applicationService.GetApplicationsCountAsync(
            CurrentUserId,
            IsPropertyManager,
            filter.Status,
            filter.PropertyId);

        var properties = await _unitService.GetPropertiesAsync();

        var model = new ApplicationListViewModel
        {
            Filter = filter,
            TotalCount = totalCount,
            IsPropertyManager = IsPropertyManager,
            Properties = properties.Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = p.Name,
                Selected = filter.PropertyId == p.Id
            }).ToList(),
            Applications = applications.Select(a => new ApplicationListItemViewModel
            {
                Id = a.Id,
                UnitId = a.UnitId,
                UnitNumber = a.Unit?.UnitNumber ?? string.Empty,
                PropertyName = a.Unit?.Property?.Name ?? string.Empty,
                MonthlyRent = a.Unit?.MonthlyRent ?? 0,
                ApplicantFullName = a.ApplicantFullName,
                ApplicantEmail = a.ApplicantEmail,
                Status = a.Status,
                CreatedAt = a.CreatedAt,
                SubmittedAt = a.SubmittedAt,
                ClaimedByName = a.ClaimedByUser?.FullName,
                IsClaimedByCurrentUser = a.ClaimedByUserId == CurrentUserId,
                CanEdit = !IsPropertyManager && a.IsEditable,
                CanWithdraw = !IsPropertyManager && !a.Status.IsTerminal(),
                CanReview = IsPropertyManager && (a.Status == ApplicationStatus.Submitted || a.Status == ApplicationStatus.UnderReview)
            }).ToList()
        };

        return View(model);
    }

    // ==========================================
    // 2. START APPLICATION (Applicant only)
    // ==========================================

    [HttpPost]
    [Authorize(Roles = DatabaseSeeder.ApplicantRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int unitId)
    {
        var applicantName = User.Identity?.Name ?? "Applicant";
        var applicantEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        var result = await _applicationService.StartApplicationAsync(unitId, CurrentUserId, applicantName, applicantEmail);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction("AvailableUnits", "Properties");
        }

        TempData["SuccessMessage"] = "Application started! Please complete Section 1.";
        return RedirectToAction(nameof(Wizard), new { id = result.Data!.Id });
    }

    // ==========================================
    // 3. SINGLE-PAGE WIZARD (GET)
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Wizard(int id, int? step = null)
    {
        var app = await _applicationService.GetApplicationByIdAsync(id, CurrentUserId, IsPropertyManager);
        if (app == null)
        {
            return NotFound();
        }

        var model = BuildWizardViewModel(app, step);
        return View(model);
    }

    // ==========================================
    // 4. PROCESS STEP (Single form posts to ONE action)
    // ==========================================

    /// <summary>
    /// Requirement 4b:
    /// "One form posts to one action, and the button clicked determines what happens:
    /// i. Continue validates the current section, persists it only when it is valid, and shows the next section (or the Summary after the last one).
    /// When the section is invalid it re-renders with the errors.
    /// ii. Back returns to the previous section without saving.
    /// Submit is available from the Summary only once both sections have been saved."
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessStep(ApplicationWizardViewModel model)
    {
        var app = await _applicationService.GetApplicationByIdAsync(model.ApplicationId, CurrentUserId, IsPropertyManager);
        if (app == null)
        {
            return NotFound();
        }

        // Server-side permission check: reject posts if editing is not allowed
        if (!app.IsEditable || IsPropertyManager)
        {
            TempData["ErrorMessage"] = "Editing is disabled for this application.";
            return RedirectToAction(nameof(Wizard), new { id = app.Id });
        }

        // ACTION: BACK
        if (model.ActionClicked == "Back")
        {
            var previousStep = Math.Max(1, model.CurrentSection - 1);
            return RedirectToAction(nameof(Wizard), new { id = app.Id, step = previousStep });
        }

        // ACTION: CONTINUE
        if (model.ActionClicked == "Continue")
        {
            if (model.CurrentSection == 1)
            {
                // Validate Section 1 (Applicant Information)
                // Remove Section 2 / general validation keys from ModelState so only Section 1 is evaluated
                ClearUnrelatedModelStateErrors("ApplicantInfo");

                if (!ModelState.IsValid)
                {
                    // Re-render Section 1 with validation errors in place
                    var reRenderModel = BuildWizardViewModel(app, sectionOverride: 1);
                    reRenderModel.ApplicantInfo = model.ApplicantInfo;
                    return View("Wizard", reRenderModel);
                }

                var saveRes = await _applicationService.SaveApplicantInfoAsync(app.Id, CurrentUserId, model.ApplicantInfo);
                if (!saveRes.Success)
                {
                    ModelState.AddModelError(string.Empty, saveRes.ErrorMessage ?? "Unable to save applicant information.");
                    var reRenderModel = BuildWizardViewModel(app, sectionOverride: 1);
                    reRenderModel.ApplicantInfo = model.ApplicantInfo;
                    return View("Wizard", reRenderModel);
                }

                TempData["SuccessMessage"] = "Section 1 saved successfully. Please enter your Residence History.";
                return RedirectToAction(nameof(Wizard), new { id = app.Id, step = 2 });
            }
            else if (model.CurrentSection == 2)
            {
                // Validate Section 2 (Residence History): verify at least 1 residence added
                var valRes = await _applicationService.ValidateResidenceHistorySectionAsync(app.Id, CurrentUserId);
                if (!valRes.Success)
                {
                    ModelState.AddModelError(string.Empty, valRes.ErrorMessage ?? "Please add at least one prior residence.");
                    var reRenderModel = BuildWizardViewModel(app, sectionOverride: 2);
                    return View("Wizard", reRenderModel);
                }

                TempData["SuccessMessage"] = "Residence History completed. Review your application summary.";
                return RedirectToAction(nameof(Wizard), new { id = app.Id, step = 3 });
            }
        }

        // ACTION: SUBMIT (Available from Summary only once both sections have been saved)
        if (model.ActionClicked == "Submit")
        {
            var submitRes = await _applicationService.SubmitApplicationAsync(app.Id, CurrentUserId);
            if (!submitRes.Success)
            {
                ModelState.AddModelError(string.Empty, submitRes.ErrorMessage ?? "Submission failed.");
                var reRenderModel = BuildWizardViewModel(app, sectionOverride: 3);
                return View("Wizard", reRenderModel);
            }

            TempData["SuccessMessage"] = "Application successfully submitted for property manager review!";
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Wizard), new { id = app.Id });
    }

    // ==========================================
    // 5. WITHDRAW (Applicant only)
    // ==========================================

    [HttpPost]
    [Authorize(Roles = DatabaseSeeder.ApplicantRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(int id, string? reason)
    {
        var result = await _applicationService.WithdrawApplicationAsync(id, CurrentUserId, reason);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Application has been withdrawn.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // 6. PROPERTY MANAGER NOTES (Bonus)
    // ==========================================

    [HttpPost]
    [Authorize(Roles = DatabaseSeeder.PropertyManagerRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote(PropertyManagerNoteInputModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Content))
        {
            TempData["ErrorMessage"] = "Note content cannot be empty.";
            return RedirectToAction(nameof(Wizard), new { id = model.RentalApplicationId });
        }

        var result = await _applicationService.AddNoteAsync(model.RentalApplicationId, CurrentUserId, model.Content);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Management note added.";
        }

        return RedirectToAction(nameof(Wizard), new { id = model.RentalApplicationId });
    }

    [HttpPost]
    [Authorize(Roles = DatabaseSeeder.PropertyManagerRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteNote(int noteId, int applicationId)
    {
        var result = await _applicationService.DeleteNoteAsync(noteId, CurrentUserId);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Management note removed.";
        }

        return RedirectToAction(nameof(Wizard), new { id = applicationId });
    }

    // ==========================================
    // HELPER: ViewModel Builder
    // ==========================================

    private ApplicationWizardViewModel BuildWizardViewModel(RentalApplication app, int? sectionOverride = null)
    {
        var isApplicant = app.ApplicantUserId == CurrentUserId;

        // Server-side read-only rule:
        // "An applicant can edit while the application is in a Draft or Returned status; otherwise, every section is read-only."
        // Property managers always see read-only application sections.
        var isReadOnly = !isApplicant || !app.IsEditable;

        int activeSection = sectionOverride ?? app.CurrentSection;
        if (activeSection < 1 || activeSection > 3) activeSection = 1;

        // If not editable, default viewing to summary section (3) unless specified
        if (isReadOnly && !sectionOverride.HasValue)
        {
            activeSection = 3;
        }

        return new ApplicationWizardViewModel
        {
            ApplicationId = app.Id,
            UnitId = app.UnitId,
            UnitNumber = app.Unit?.UnitNumber ?? string.Empty,
            PropertyName = app.Unit?.Property?.Name ?? string.Empty,
            PropertyAddress = $"{app.Unit?.Property?.Address}, {app.Unit?.Property?.City}, {app.Unit?.Property?.State} {app.Unit?.Property?.ZipCode}",
            MonthlyRent = app.Unit?.MonthlyRent ?? 0,
            Bedrooms = app.Unit?.Bedrooms ?? 0,
            Bathrooms = app.Unit?.Bathrooms ?? 1.0m,
            UnitTypeName = app.Unit?.UnitType?.Name ?? string.Empty,
            Status = app.Status,
            CreatedAt = app.CreatedAt,
            SubmittedAt = app.SubmittedAt,
            CurrentSection = activeSection,
            IsReadOnly = isReadOnly,
            IsPropertyManager = IsPropertyManager,
            CanWithdraw = isApplicant && !app.Status.IsTerminal(),
            ApplicantInfoSaved = app.ApplicantInfoSaved,
            ResidenceHistorySaved = app.ResidenceHistorySaved,
            ApplicantInfo = new ApplicantInfoInputModel
            {
                ApplicantFullName = string.IsNullOrEmpty(app.ApplicantFullName) ? (app.ApplicantUser?.FullName ?? string.Empty) : app.ApplicantFullName,
                ApplicantEmail = string.IsNullOrEmpty(app.ApplicantEmail) ? (app.ApplicantUser?.Email ?? string.Empty) : app.ApplicantEmail,
                ApplicantPhone = app.ApplicantPhone,
                CurrentAddress = app.CurrentAddress,
                CurrentCity = app.CurrentCity,
                CurrentState = app.CurrentState,
                CurrentZipCode = app.CurrentZipCode
            },
            Residences = app.Residences.Select(r => new ResidenceItemViewModel
            {
                Id = r.Id,
                RentalApplicationId = r.RentalApplicationId,
                Address = r.Address,
                City = r.City,
                State = r.State,
                ZipCode = r.ZipCode,
                LandlordName = r.LandlordName,
                LandlordPhone = r.LandlordPhone,
                MoveInDate = r.MoveInDate,
                MoveOutDate = r.MoveOutDate
            }).OrderByDescending(r => r.MoveInDate).ToList(),
            StatusHistory = app.StatusHistories.Select(h => new ApplicationStatusHistoryItemViewModel
            {
                Id = h.Id,
                OldStatus = h.OldStatus,
                NewStatus = h.NewStatus,
                ChangedByName = h.ChangedByUser?.FullName ?? "System",
                ChangedAt = h.ChangedAt,
                Comment = h.Comment
            }).OrderByDescending(h => h.ChangedAt).ToList(),
            PropertyManagerNotes = IsPropertyManager
                ? app.Notes.Select(n => new NoteItemViewModel
                {
                    Id = n.Id,
                    RentalApplicationId = n.RentalApplicationId,
                    AuthorName = n.AuthorUser?.FullName ?? "Staff",
                    Content = n.Content,
                    CreatedAt = n.CreatedAt
                }).OrderByDescending(n => n.CreatedAt).ToList()
                : new List<NoteItemViewModel>(),
            ClaimedByName = app.ClaimedByUser?.FullName,
            IsClaimedByCurrentUser = app.ClaimedByUserId == CurrentUserId,
            ReviewModel = new ReviewApplicationViewModel
            {
                ApplicationId = app.Id,
                ApplicantName = app.ApplicantFullName,
                PropertyName = app.Unit?.Property?.Name ?? string.Empty,
                UnitDisplay = $"Unit {app.Unit?.UnitNumber}"
            }
        };
    }

    private void ClearUnrelatedModelStateErrors(string targetPrefix)
    {
        var keysToRemove = ModelState.Keys
            .Where(k => !k.StartsWith(targetPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            ModelState.Remove(key);
        }
    }
}
