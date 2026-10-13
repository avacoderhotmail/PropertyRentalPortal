using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyRentalPortal.Web.Data.Enums;
using PropertyRentalPortal.Web.Data.Seeding;
using PropertyRentalPortal.Web.Models;
using PropertyRentalPortal.Web.Services;

namespace PropertyRentalPortal.Web.Controllers;

[Authorize(Roles = DatabaseSeeder.PropertyManagerRole)]
public class ReviewController : Controller
{
    private readonly IApplicationService _applicationService;

    public ReviewController(IApplicationService applicationService)
    {
        _applicationService = applicationService;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // Modal: Open Review
    [HttpGet]
    public async Task<IActionResult> ReviewModal(int id)
    {
        var app = await _applicationService.GetApplicationByIdAsync(id, CurrentUserId, isManager: true);
        if (app == null) return NotFound();

        var model = new ReviewApplicationViewModel
        {
            ApplicationId = app.Id,
            ApplicantName = app.ApplicantFullName,
            PropertyName = app.Unit?.Property?.Name ?? string.Empty,
            UnitDisplay = $"Unit {app.Unit?.UnitNumber}",
            Outcome = ReviewOutcome.Approve
        };

        return PartialView("_ReviewModalPartial", model);
    }

    // Modal POST: Complete Review
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteReview(ReviewApplicationViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_ReviewModalPartial", model);
        }

        var result = await _applicationService.ReviewApplicationAsync(
            model.ApplicationId,
            CurrentUserId,
            model.Outcome,
            model.Comment);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not complete review.");
            return PartialView("_ReviewModalPartial", model);
        }

        return Json(new
        {
            success = true,
            message = model.Outcome switch
            {
                ReviewOutcome.Approve => "Application APPROVED! A 12-month lease has been issued for the unit.",
                ReviewOutcome.Return => "Application RETURNED to applicant for corrections.",
                ReviewOutcome.Deny => "Application DENIED.",
                _ => "Review recorded successfully."
            }
        });
    }

    // Review Queue: Claim
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Claim(int applicationId)
    {
        var result = await _applicationService.ClaimApplicationAsync(applicationId, CurrentUserId);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Application claimed into your review queue.";
        }

        return RedirectToAction("Index", "Applications");
    }

    // Review Queue: Release
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Release(int applicationId)
    {
        var result = await _applicationService.ReleaseApplicationAsync(applicationId, CurrentUserId);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Application released back to the general queue.";
        }

        return RedirectToAction("Index", "Applications");
    }
}
