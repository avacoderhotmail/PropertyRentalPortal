using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyRentalPortal.Web.Data.Seeding;
using PropertyRentalPortal.Web.Models;
using PropertyRentalPortal.Web.Services;

namespace PropertyRentalPortal.Web.Controllers;

[Authorize(Roles = DatabaseSeeder.ApplicantRole)]
public class ResidencesController : Controller
{
    private readonly IApplicationService _applicationService;

    public ResidencesController(IApplicationService applicationService)
    {
        _applicationService = applicationService;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // Modal: Add Prior Residence
    [HttpGet]
    public IActionResult CreateModal(int applicationId)
    {
        var model = new ResidenceHistoryInputModel
        {
            RentalApplicationId = applicationId,
            MoveInDate = DateTime.UtcNow.AddYears(-1).Date
        };
        return PartialView("_ResidenceModalPartial", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ResidenceHistoryInputModel model)
    {
        if (model.MoveOutDate.HasValue && model.MoveOutDate.Value < model.MoveInDate)
        {
            ModelState.AddModelError(nameof(model.MoveOutDate), "Move-out date cannot be earlier than move-in date.");
        }

        if (!ModelState.IsValid)
        {
            return PartialView("_ResidenceModalPartial", model);
        }

        var result = await _applicationService.AddResidenceAsync(model.RentalApplicationId, CurrentUserId, model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not add residence.");
            return PartialView("_ResidenceModalPartial", model);
        }

        return Json(new { success = true, message = "Residence added successfully!" });
    }

    // Modal: Edit Prior Residence
    [HttpGet]
    public async Task<IActionResult> EditModal(int id)
    {
        var residence = await _applicationService.GetResidenceByIdAsync(id);
        if (residence == null) return NotFound();

        var model = new ResidenceHistoryInputModel
        {
            Id = residence.Id,
            RentalApplicationId = residence.RentalApplicationId,
            Address = residence.Address,
            City = residence.City,
            State = residence.State,
            ZipCode = residence.ZipCode,
            LandlordName = residence.LandlordName,
            LandlordPhone = residence.LandlordPhone,
            MoveInDate = residence.MoveInDate,
            MoveOutDate = residence.MoveOutDate
        };

        return PartialView("_ResidenceModalPartial", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ResidenceHistoryInputModel model)
    {
        if (model.MoveOutDate.HasValue && model.MoveOutDate.Value < model.MoveInDate)
        {
            ModelState.AddModelError(nameof(model.MoveOutDate), "Move-out date cannot be earlier than move-in date.");
        }

        if (!ModelState.IsValid)
        {
            return PartialView("_ResidenceModalPartial", model);
        }

        var result = await _applicationService.UpdateResidenceAsync(model.Id, CurrentUserId, model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not update residence.");
            return PartialView("_ResidenceModalPartial", model);
        }

        return Json(new { success = true, message = "Residence updated successfully!" });
    }

    // Delete Residence
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _applicationService.DeleteResidenceAsync(id, CurrentUserId);
        if (!result.Success)
        {
            return Json(new { success = false, message = result.ErrorMessage });
        }

        return Json(new { success = true, message = "Residence removed." });
    }
}
