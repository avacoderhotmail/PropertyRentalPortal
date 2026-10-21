using Microsoft.EntityFrameworkCore;
using PropertyRentalPortal.Web.Data;
using PropertyRentalPortal.Web.Data.Entities;
using PropertyRentalPortal.Web.Data.Enums;
using PropertyRentalPortal.Web.Models;
using PropertyRentalPortal.Web.Services.Common;

namespace PropertyRentalPortal.Web.Services;

public class ApplicationService : IApplicationService
{
    private readonly ApplicationDbContext _context;

    public ApplicationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RentalApplication?> GetApplicationByIdAsync(int id, string? requestingUserId = null, bool isManager = false)
    {
        var query = _context.RentalApplications
            .Include(a => a.Unit)
                .ThenInclude(u => u.Property)
            .Include(a => a.Unit)
                .ThenInclude(u => u.UnitType)
            .Include(a => a.Unit)
                .ThenInclude(u => u.Leases)
            .Include(a => a.ApplicantUser)
            .Include(a => a.ClaimedByUser)
            .Include(a => a.Residences)
            .Include(a => a.StatusHistories)
                .ThenInclude(h => h.ChangedByUser)
            .Include(a => a.Notes)
                .ThenInclude(n => n.AuthorUser)
            .Include(a => a.Lease)
            .AsSplitQuery();

        if (!isManager && !string.IsNullOrEmpty(requestingUserId))
        {
            query = query.Where(a => a.ApplicantUserId == requestingUserId);
        }

        return await query.FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<List<RentalApplication>> GetApplicationsListAsync(
        string? userId,
        bool isManager,
        ApplicationStatus? status = null,
        int? propertyId = null,
        string? sortBy = null,
        bool sortDescending = false,
        int page = 1,
        int pageSize = 10)
    {
        var query = BuildApplicationQuery(userId, isManager, status, propertyId);

        // Database-side sorting
        query = (sortBy?.ToLower()) switch
        {
            "applicantname" => sortDescending
                ? query.OrderByDescending(a => a.ApplicantFullName)
                : query.OrderBy(a => a.ApplicantFullName),
            "property" => sortDescending
                ? query.OrderByDescending(a => a.Unit.Property.Name).ThenByDescending(a => a.Unit.UnitNumber)
                : query.OrderBy(a => a.Unit.Property.Name).ThenBy(a => a.Unit.UnitNumber),
            "status" => sortDescending
                ? query.OrderByDescending(a => a.Status)
                : query.OrderBy(a => a.Status),
            "submittedat" => sortDescending
                ? query.OrderByDescending(a => a.SubmittedAt)
                : query.OrderBy(a => a.SubmittedAt),
            _ => sortDescending
                ? query.OrderByDescending(a => a.CreatedAt)
                : query.OrderBy(a => a.CreatedAt)
        };

        // Database-side pagination
        return await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetApplicationsCountAsync(
        string? userId,
        bool isManager,
        ApplicationStatus? status = null,
        int? propertyId = null)
    {
        var query = BuildApplicationQuery(userId, isManager, status, propertyId);
        return await query.CountAsync();
    }

    private IQueryable<RentalApplication> BuildApplicationQuery(
        string? userId,
        bool isManager,
        ApplicationStatus? status,
        int? propertyId)
    {
        var query = _context.RentalApplications
            .Include(a => a.Unit)
                .ThenInclude(u => u.Property)
            .Include(a => a.Unit)
                .ThenInclude(u => u.UnitType)
            .Include(a => a.ApplicantUser)
            .Include(a => a.ClaimedByUser)
            .AsQueryable();

        // Security filter: Applicants see only their own applications; Property Managers see all
        if (!isManager)
        {
            query = query.Where(a => a.ApplicantUserId == userId);
        }

        // Database filtering by status
        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        // Database filtering by property
        if (propertyId.HasValue)
        {
            query = query.Where(a => a.Unit.PropertyId == propertyId.Value);
        }

        return query;
    }

    public async Task<ServiceResult<RentalApplication>> StartApplicationAsync(
        int unitId,
        string applicantUserId,
        string applicantName,
        string applicantEmail)
    {
        var unit = await _context.Units
            .Include(u => u.Leases)
            .FirstOrDefaultAsync(u => u.Id == unitId);

        if (unit == null)
            return ServiceResult<RentalApplication>.Fail("Unit not found.");

        var today = DateTime.UtcNow.Date;
        var hasActiveLease = unit.Leases.Any(l => l.StartDate.Date <= today && today <= l.EndDate.Date);
        if (hasActiveLease)
            return ServiceResult<RentalApplication>.Fail("This unit is not available because it has an active lease.");

        var application = new RentalApplication
        {
            UnitId = unitId,
            ApplicantUserId = applicantUserId,
            Status = ApplicationStatus.Draft,
            CurrentSection = 1,
            ApplicantFullName = applicantName,
            ApplicantEmail = applicantEmail,
            CreatedAt = DateTime.UtcNow
        };

        application.StatusHistories.Add(new ApplicationStatusHistory
        {
            OldStatus = null,
            NewStatus = ApplicationStatus.Draft,
            ChangedByUserId = applicantUserId,
            ChangedAt = DateTime.UtcNow,
            Comment = "Created draft rental application."
        });

        _context.RentalApplications.Add(application);
        await _context.SaveChangesAsync();

        return ServiceResult<RentalApplication>.Ok(application);
    }

    public async Task<ServiceResult<RentalApplication>> SaveApplicantInfoAsync(
        int applicationId,
        string applicantUserId,
        ApplicantInfoInputModel model)
    {
        var app = await _context.RentalApplications
            .Include(a => a.Unit)
            .Include(a => a.Residences)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (app == null)
            return ServiceResult<RentalApplication>.Fail("Application not found.");

        if (app.ApplicantUserId != applicantUserId)
            return ServiceResult<RentalApplication>.Fail("Unauthorized access to this application.");

        if (!app.IsEditable)
            return ServiceResult<RentalApplication>.Fail($"Application cannot be edited in its current status ({app.Status}).");

        // Update Section 1 data
        app.ApplicantFullName = model.ApplicantFullName;
        app.ApplicantEmail = model.ApplicantEmail;
        app.ApplicantPhone = model.ApplicantPhone;
        app.CurrentAddress = model.CurrentAddress;
        app.CurrentCity = model.CurrentCity;
        app.CurrentState = model.CurrentState;
        app.CurrentZipCode = model.CurrentZipCode;

        app.ApplicantInfoSaved = true;
        app.CurrentSection = 2; // Advance to Section 2: Residence History

        await _context.SaveChangesAsync();
        return ServiceResult<RentalApplication>.Ok(app);
    }

    public async Task<ServiceResult<RentalApplication>> ValidateResidenceHistorySectionAsync(
        int applicationId,
        string applicantUserId)
    {
        var app = await _context.RentalApplications
            .Include(a => a.Unit)
            .Include(a => a.Residences)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (app == null)
            return ServiceResult<RentalApplication>.Fail("Application not found.");

        if (app.ApplicantUserId != applicantUserId)
            return ServiceResult<RentalApplication>.Fail("Unauthorized access to this application.");

        if (!app.IsEditable)
            return ServiceResult<RentalApplication>.Fail($"Application cannot be edited in its current status ({app.Status}).");

        if (!app.Residences.Any())
        {
            return ServiceResult<RentalApplication>.Fail("Please add at least one prior residence before continuing to the summary.");
        }

        app.ResidenceHistorySaved = true;
        app.CurrentSection = 3; // Advance to Summary

        await _context.SaveChangesAsync();
        return ServiceResult<RentalApplication>.Ok(app);
    }

    public async Task<ServiceResult<RentalApplication>> SubmitApplicationAsync(
        int applicationId,
        string applicantUserId)
    {
        var app = await _context.RentalApplications
            .Include(a => a.Unit)
                .ThenInclude(u => u.Leases)
            .Include(a => a.Residences)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (app == null)
            return ServiceResult<RentalApplication>.Fail("Application not found.");

        if (app.ApplicantUserId != applicantUserId)
            return ServiceResult<RentalApplication>.Fail("Unauthorized access to this application.");

        if (!app.IsEditable)
            return ServiceResult<RentalApplication>.Fail($"Only Draft or Returned applications can be submitted (current status: {app.Status}).");

        // Requirement: "Submit is available from the Summary only once both sections have been saved."
        if (!app.ApplicantInfoSaved || !app.ResidenceHistorySaved)
            return ServiceResult<RentalApplication>.Fail("Both Applicant Information and Residence History sections must be saved prior to submission.");

        // Requirement: "At submit and again at approval, reject the action with an error when the unit has an active lease. Other open applications for that unit are left as they are. The approval check prevents a second lease."
        var today = DateTime.UtcNow.Date;
        var hasActiveLease = app.Unit.Leases.Any(l => l.StartDate.Date <= today && today <= l.EndDate.Date);
        if (hasActiveLease)
            return ServiceResult<RentalApplication>.Fail("Cannot submit application: This unit has an active lease and is no longer available.");

        var oldStatus = app.Status;
        app.Status = ApplicationStatus.Submitted;
        app.SubmittedAt = DateTime.UtcNow;
        app.CurrentSection = 3;

        app.StatusHistories.Add(new ApplicationStatusHistory
        {
            OldStatus = oldStatus,
            NewStatus = ApplicationStatus.Submitted,
            ChangedByUserId = applicantUserId,
            ChangedAt = DateTime.UtcNow,
            Comment = oldStatus == ApplicationStatus.Returned
                ? "Resubmitted application after addressing returned feedback."
                : "Submitted completed application for review."
        });

        await _context.SaveChangesAsync();
        return ServiceResult<RentalApplication>.Ok(app);
    }

    public async Task<ServiceResult> WithdrawApplicationAsync(
        int applicationId,
        string applicantUserId,
        string? reason)
    {
        var app = await _context.RentalApplications
            .Include(a => a.StatusHistories)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (app == null)
            return ServiceResult.Fail("Application not found.");

        if (app.ApplicantUserId != applicantUserId)
            return ServiceResult.Fail("Unauthorized access to this application.");

        if (app.Status.IsTerminal())
            return ServiceResult.Fail($"Cannot withdraw application in terminal status ({app.Status}).");

        var oldStatus = app.Status;
        app.Status = ApplicationStatus.Withdrawn;
        app.CompletedAt = DateTime.UtcNow;

        app.StatusHistories.Add(new ApplicationStatusHistory
        {
            OldStatus = oldStatus,
            NewStatus = ApplicationStatus.Withdrawn,
            ChangedByUserId = applicantUserId,
            ChangedAt = DateTime.UtcNow,
            Comment = string.IsNullOrWhiteSpace(reason) ? "Application withdrawn by applicant." : $"Withdrawn: {reason}"
        });

        await _context.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    // --- Residence History Modal CRUD ---
    public async Task<ResidenceHistory?> GetResidenceByIdAsync(int residenceId)
    {
        return await _context.ResidenceHistories
            .Include(r => r.RentalApplication)
            .FirstOrDefaultAsync(r => r.Id == residenceId);
    }

    public async Task<ServiceResult<ResidenceHistory>> AddResidenceAsync(
        int applicationId,
        string applicantUserId,
        ResidenceHistoryInputModel model)
    {
        var app = await _context.RentalApplications.FindAsync(applicationId);
        if (app == null)
            return ServiceResult<ResidenceHistory>.Fail("Application not found.");

        if (app.ApplicantUserId != applicantUserId)
            return ServiceResult<ResidenceHistory>.Fail("Unauthorized access to this application.");

        if (!app.IsEditable)
            return ServiceResult<ResidenceHistory>.Fail("Cannot add residences to an application that is not in Draft or Returned status.");

        if (model.MoveOutDate.HasValue && model.MoveOutDate.Value < model.MoveInDate)
            return ServiceResult<ResidenceHistory>.Fail("Move-out date cannot be earlier than move-in date.");

        var residence = new ResidenceHistory
        {
            RentalApplicationId = applicationId,
            Address = model.Address,
            City = model.City,
            State = model.State,
            ZipCode = model.ZipCode,
            LandlordName = model.LandlordName,
            LandlordPhone = model.LandlordPhone,
            MoveInDate = model.MoveInDate,
            MoveOutDate = model.MoveOutDate,
            CreatedAt = DateTime.UtcNow
        };

        _context.ResidenceHistories.Add(residence);
        await _context.SaveChangesAsync();
        return ServiceResult<ResidenceHistory>.Ok(residence);
    }

    public async Task<ServiceResult<ResidenceHistory>> UpdateResidenceAsync(
        int residenceId,
        string applicantUserId,
        ResidenceHistoryInputModel model)
    {
        var residence = await _context.ResidenceHistories
            .Include(r => r.RentalApplication)
            .FirstOrDefaultAsync(r => r.Id == residenceId);

        if (residence == null)
            return ServiceResult<ResidenceHistory>.Fail("Residence not found.");

        if (residence.RentalApplication.ApplicantUserId != applicantUserId)
            return ServiceResult<ResidenceHistory>.Fail("Unauthorized access.");

        if (!residence.RentalApplication.IsEditable)
            return ServiceResult<ResidenceHistory>.Fail("Cannot edit residences for an application that is not in Draft or Returned status.");

        if (model.MoveOutDate.HasValue && model.MoveOutDate.Value < model.MoveInDate)
            return ServiceResult<ResidenceHistory>.Fail("Move-out date cannot be earlier than move-in date.");

        residence.Address = model.Address;
        residence.City = model.City;
        residence.State = model.State;
        residence.ZipCode = model.ZipCode;
        residence.LandlordName = model.LandlordName;
        residence.LandlordPhone = model.LandlordPhone;
        residence.MoveInDate = model.MoveInDate;
        residence.MoveOutDate = model.MoveOutDate;

        await _context.SaveChangesAsync();
        return ServiceResult<ResidenceHistory>.Ok(residence);
    }

    public async Task<ServiceResult> DeleteResidenceAsync(int residenceId, string applicantUserId)
    {
        var residence = await _context.ResidenceHistories
            .Include(r => r.RentalApplication)
            .FirstOrDefaultAsync(r => r.Id == residenceId);

        if (residence == null)
            return ServiceResult.Fail("Residence not found.");

        if (residence.RentalApplication.ApplicantUserId != applicantUserId)
            return ServiceResult.Fail("Unauthorized access.");

        if (!residence.RentalApplication.IsEditable)
            return ServiceResult.Fail("Cannot delete residences for an application that is not in Draft or Returned status.");

        _context.ResidenceHistories.Remove(residence);
        await _context.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    // --- Property Manager Review ---
    public async Task<ServiceResult<RentalApplication>> ReviewApplicationAsync(
        int applicationId,
        string managerUserId,
        ReviewOutcome outcome,
        string? comment)
    {
        var app = await _context.RentalApplications
            .Include(a => a.Unit)
                .ThenInclude(u => u.Leases)
            .Include(a => a.StatusHistories)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (app == null)
            return ServiceResult<RentalApplication>.Fail("Application not found.");

        if (app.Status != ApplicationStatus.Submitted && app.Status != ApplicationStatus.UnderReview)
            return ServiceResult<RentalApplication>.Fail($"Only submitted or under review applications can be reviewed (current status: {app.Status}).");

        // Requirement: "comment (required for Return and Deny)"
        if ((outcome == ReviewOutcome.Return || outcome == ReviewOutcome.Deny) && string.IsNullOrWhiteSpace(comment))
        {
            return ServiceResult<RentalApplication>.Fail($"A comment is required when {(outcome == ReviewOutcome.Return ? "returning" : "denying")} an application.");
        }

        var oldStatus = app.Status;
        var today = DateTime.UtcNow.Date;

        if (outcome == ReviewOutcome.Approve)
        {
            // Requirement: "At submit and again at approval, reject the action with an error when the unit has an active lease. Other open applications for that unit are left as they are. The approval check prevents a second lease."
            var hasActiveLease = app.Unit.Leases.Any(l => l.StartDate.Date <= today && today <= l.EndDate.Date);
            if (hasActiveLease)
            {
                return ServiceResult<RentalApplication>.Fail("Approval rejected: The unit already has an active lease covering today.");
            }

            // Requirement: "An approved application issues a twelve-month lease for the unit. Approval creates a lease for the unit with a start date and a twelve-month term."
            var leaseStartDate = today;
            var leaseEndDate = leaseStartDate.AddYears(1).AddDays(-1);

            var lease = new Lease
            {
                UnitId = app.UnitId,
                TenantUserId = app.ApplicantUserId,
                RentalApplication = app,
                StartDate = leaseStartDate,
                EndDate = leaseEndDate,
                MonthlyRent = app.Unit.MonthlyRent,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Leases.Add(lease);

            app.Status = ApplicationStatus.Approved;
            app.CompletedAt = DateTime.UtcNow;
            app.Lease = lease;
        }
        else if (outcome == ReviewOutcome.Return)
        {
            app.Status = ApplicationStatus.Returned;
        }
        else if (outcome == ReviewOutcome.Deny)
        {
            app.Status = ApplicationStatus.Denied;
            app.CompletedAt = DateTime.UtcNow;
        }

        // Record history
        app.StatusHistories.Add(new ApplicationStatusHistory
        {
            OldStatus = oldStatus,
            NewStatus = app.Status,
            ChangedByUserId = managerUserId,
            ChangedAt = DateTime.UtcNow,
            Comment = comment ?? $"Application marked as {app.Status}."
        });

        // Release review claim if was claimed
        app.ClaimedByUserId = null;
        app.ClaimedAt = null;

        await _context.SaveChangesAsync();
        return ServiceResult<RentalApplication>.Ok(app);
    }

    // --- Bonus: Review Queue ---
    public async Task<ServiceResult> ClaimApplicationAsync(int applicationId, string managerUserId)
    {
        var app = await _context.RentalApplications.FindAsync(applicationId);
        if (app == null)
            return ServiceResult.Fail("Application not found.");

        if (app.Status != ApplicationStatus.Submitted)
            return ServiceResult.Fail($"Only submitted applications can be claimed into the review queue (current: {app.Status}).");

        app.Status = ApplicationStatus.UnderReview;
        app.ClaimedByUserId = managerUserId;
        app.ClaimedAt = DateTime.UtcNow;

        app.StatusHistories.Add(new ApplicationStatusHistory
        {
            OldStatus = ApplicationStatus.Submitted,
            NewStatus = ApplicationStatus.UnderReview,
            ChangedByUserId = managerUserId,
            ChangedAt = DateTime.UtcNow,
            Comment = "Claimed application for review."
        });

        await _context.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ReleaseApplicationAsync(int applicationId, string managerUserId)
    {
        var app = await _context.RentalApplications.FindAsync(applicationId);
        if (app == null)
            return ServiceResult.Fail("Application not found.");

        if (app.Status != ApplicationStatus.UnderReview)
            return ServiceResult.Fail("Application is not under review.");

        app.Status = ApplicationStatus.Submitted;
        app.ClaimedByUserId = null;
        app.ClaimedAt = null;

        app.StatusHistories.Add(new ApplicationStatusHistory
        {
            OldStatus = ApplicationStatus.UnderReview,
            NewStatus = ApplicationStatus.Submitted,
            ChangedByUserId = managerUserId,
            ChangedAt = DateTime.UtcNow,
            Comment = "Released application back to review queue."
        });

        await _context.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    // --- Bonus: Property Manager Notes ---
    public async Task<List<PropertyManagerNote>> GetNotesAsync(int applicationId)
    {
        return await _context.PropertyManagerNotes
            .Include(n => n.AuthorUser)
            .Where(n => n.RentalApplicationId == applicationId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<ServiceResult<PropertyManagerNote>> AddNoteAsync(int applicationId, string managerUserId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return ServiceResult<PropertyManagerNote>.Fail("Note content cannot be empty.");

        var note = new PropertyManagerNote
        {
            RentalApplicationId = applicationId,
            AuthorUserId = managerUserId,
            Content = content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.PropertyManagerNotes.Add(note);
        await _context.SaveChangesAsync();
        return ServiceResult<PropertyManagerNote>.Ok(note);
    }

    public async Task<ServiceResult> DeleteNoteAsync(int noteId, string managerUserId)
    {
        var note = await _context.PropertyManagerNotes.FindAsync(noteId);
        if (note == null)
            return ServiceResult.Fail("Note not found.");

        _context.PropertyManagerNotes.Remove(note);
        await _context.SaveChangesAsync();
        return ServiceResult.Ok();
    }
}
