using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PropertyRentalPortal.Web.Data.Entities;
using PropertyRentalPortal.Web.Data.Seeding;
using PropertyRentalPortal.Web.Models;
using PropertyRentalPortal.Web.Services;

namespace PropertyRentalPortal.Web.Controllers;

public class PropertiesController : Controller
{
    private readonly IUnitService _unitService;

    public PropertiesController(IUnitService unitService)
    {
        _unitService = unitService;
    }

    // ==========================================
    // 1. PROPERTY MANAGER: Properties & Units
    // ==========================================

    [Authorize(Roles = DatabaseSeeder.PropertyManagerRole)]
    public async Task<IActionResult> Index()
    {
        var properties = await _unitService.GetPropertiesAsync();
        var model = properties.Select(p => new PropertyListItemViewModel
        {
            Id = p.Id,
            Name = p.Name,
            FullAddress = $"{p.Address}, {p.City}, {p.State} {p.ZipCode}",
            Description = p.Description,
            TotalUnits = p.Units.Count,
            AvailableUnits = p.Units.Count(u => !u.HasActiveLease())
        }).ToList();

        return View(model);
    }

    [Authorize(Roles = DatabaseSeeder.PropertyManagerRole)]
    public async Task<IActionResult> Details(int id)
    {
        var property = await _unitService.GetPropertyByIdAsync(id);
        if (property == null) return NotFound();

        ViewData["Property"] = property;
        var units = property.Units.Select(u =>
        {
            var activeLease = u.Leases.FirstOrDefault(l => l.StartDate.Date <= DateTime.UtcNow.Date && DateTime.UtcNow.Date <= l.EndDate.Date);
            return new UnitListItemViewModel
            {
                Id = u.Id,
                PropertyId = u.PropertyId,
                PropertyName = property.Name,
                UnitNumber = u.UnitNumber,
                Bedrooms = u.Bedrooms,
                Bathrooms = u.Bathrooms,
                MonthlyRent = u.MonthlyRent,
                UnitTypeName = u.UnitType?.Name ?? "Standard",
                IsUnitTypeActive = u.UnitType?.IsActive ?? true,
                IsAvailable = activeLease == null,
                ActiveLeaseEndDate = activeLease?.EndDate
            };
        }).OrderBy(u => u.UnitNumber).ToList();

        return View(units);
    }

    // Modal: Create Property
    [HttpGet]
    [Authorize(Roles = DatabaseSeeder.PropertyManagerRole)]
    public IActionResult CreatePropertyModal()
    {
        return PartialView("_PropertyModalPartial", new PropertyFormViewModel());
    }

    [HttpPost]
    [Authorize(Roles = DatabaseSeeder.PropertyManagerRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProperty(PropertyFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_PropertyModalPartial", model);
        }

        var entity = new Property
        {
            Name = model.Name,
            Address = model.Address,
            City = model.City,
            State = model.State,
            ZipCode = model.ZipCode,
            Description = model.Description
        };

        var result = await _unitService.CreatePropertyAsync(entity);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not create property.");
            return PartialView("_PropertyModalPartial", model);
        }

        return Json(new { success = true, message = "Property created successfully!" });
    }

    // Modal: Edit Property
    [HttpGet]
    [Authorize(Roles = DatabaseSeeder.PropertyManagerRole)]
    public async Task<IActionResult> EditPropertyModal(int id)
    {
        var property = await _unitService.GetPropertyByIdAsync(id);
        if (property == null) return NotFound();

        var model = new PropertyFormViewModel
        {
            Id = property.Id,
            Name = property.Name,
            Address = property.Address,
            City = property.City,
            State = property.State,
            ZipCode = property.ZipCode,
            Description = property.Description
        };

        return PartialView("_PropertyModalPartial", model);
    }

    [HttpPost]
    [Authorize(Roles = DatabaseSeeder.PropertyManagerRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProperty(PropertyFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_PropertyModalPartial", model);
        }

        var entity = new Property
        {
            Id = model.Id,
            Name = model.Name,
            Address = model.Address,
            City = model.City,
            State = model.State,
            ZipCode = model.ZipCode,
            Description = model.Description
        };

        var result = await _unitService.UpdatePropertyAsync(entity);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not update property.");
            return PartialView("_PropertyModalPartial", model);
        }

        return Json(new { success = true, message = "Property updated successfully!" });
    }

    [HttpPost]
    [Authorize(Roles = DatabaseSeeder.PropertyManagerRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProperty(int id)
    {
        var result = await _unitService.DeletePropertyAsync(id);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Property deleted successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    // Modal: Create Unit
    [HttpGet]
    [Authorize(Roles = DatabaseSeeder.PropertyManagerRole)]
    public async Task<IActionResult> CreateUnitModal(int propertyId)
    {
        var property = await _unitService.GetPropertyByIdAsync(propertyId);
        if (property == null) return NotFound();

        var unitTypes = await _unitService.GetUnitTypesForSelectionAsync();
        var model = new UnitFormViewModel
        {
            PropertyId = propertyId,
            PropertyName = property.Name,
            AvailableUnitTypes = unitTypes.Select(ut => new SelectListItem
            {
                Value = ut.Id.ToString(),
                Text = ut.Name
            }).ToList()
        };

        return PartialView("_UnitModalPartial", model);
    }

    [HttpPost]
    [Authorize(Roles = DatabaseSeeder.PropertyManagerRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUnit(UnitFormViewModel model)
    {
        var unitTypes = await _unitService.GetUnitTypesForSelectionAsync();
        model.AvailableUnitTypes = unitTypes.Select(ut => new SelectListItem
        {
            Value = ut.Id.ToString(),
            Text = ut.Name
        }).ToList();

        if (!ModelState.IsValid)
        {
            return PartialView("_UnitModalPartial", model);
        }

        var unit = new Unit
        {
            PropertyId = model.PropertyId,
            UnitNumber = model.UnitNumber,
            Bedrooms = model.Bedrooms,
            Bathrooms = model.Bathrooms,
            MonthlyRent = model.MonthlyRent,
            UnitTypeId = model.UnitTypeId
        };

        var result = await _unitService.CreateUnitAsync(unit);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not create unit.");
            return PartialView("_UnitModalPartial", model);
        }

        return Json(new { success = true, message = "Unit created successfully!" });
    }

    // Modal: Edit Unit
    [HttpGet]
    [Authorize(Roles = DatabaseSeeder.PropertyManagerRole)]
    public async Task<IActionResult> EditUnitModal(int id)
    {
        var unit = await _unitService.GetUnitByIdAsync(id);
        if (unit == null) return NotFound();

        // Pass current unit's unitTypeId so inactive types already used by this unit still display!
        var unitTypes = await _unitService.GetUnitTypesForSelectionAsync(unit.UnitTypeId);
        var model = new UnitFormViewModel
        {
            Id = unit.Id,
            PropertyId = unit.PropertyId,
            PropertyName = unit.Property.Name,
            UnitNumber = unit.UnitNumber,
            Bedrooms = unit.Bedrooms,
            Bathrooms = unit.Bathrooms,
            MonthlyRent = unit.MonthlyRent,
            UnitTypeId = unit.UnitTypeId,
            AvailableUnitTypes = unitTypes.Select(ut => new SelectListItem
            {
                Value = ut.Id.ToString(),
                Text = ut.IsActive ? ut.Name : $"{ut.Name} (Inactive - Assigned)",
                Selected = ut.Id == unit.UnitTypeId
            }).ToList()
        };

        return PartialView("_UnitModalPartial", model);
    }

    [HttpPost]
    [Authorize(Roles = DatabaseSeeder.PropertyManagerRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUnit(UnitFormViewModel model)
    {
        var unitTypes = await _unitService.GetUnitTypesForSelectionAsync(model.UnitTypeId);
        model.AvailableUnitTypes = unitTypes.Select(ut => new SelectListItem
        {
            Value = ut.Id.ToString(),
            Text = ut.IsActive ? ut.Name : $"{ut.Name} (Inactive - Assigned)",
            Selected = ut.Id == model.UnitTypeId
        }).ToList();

        if (!ModelState.IsValid)
        {
            return PartialView("_UnitModalPartial", model);
        }

        var unit = new Unit
        {
            Id = model.Id,
            PropertyId = model.PropertyId,
            UnitNumber = model.UnitNumber,
            Bedrooms = model.Bedrooms,
            Bathrooms = model.Bathrooms,
            MonthlyRent = model.MonthlyRent,
            UnitTypeId = model.UnitTypeId
        };

        var result = await _unitService.UpdateUnitAsync(unit);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not update unit.");
            return PartialView("_UnitModalPartial", model);
        }

        return Json(new { success = true, message = "Unit updated successfully!" });
    }

    [HttpPost]
    [Authorize(Roles = DatabaseSeeder.PropertyManagerRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUnit(int id, int propertyId)
    {
        var result = await _unitService.DeleteUnitAsync(id);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
        }
        else
        {
            TempData["SuccessMessage"] = "Unit deleted successfully.";
        }

        return RedirectToAction(nameof(Details), new { id = propertyId });
    }

    // ==========================================
    // 2. APPLICANT: Browse Available Units
    // ==========================================

    [AllowAnonymous]
    public async Task<IActionResult> AvailableUnits(int? propertyId = null)
    {
        var properties = await _unitService.GetPropertiesAsync();
        var units = await _unitService.GetUnitsAsync(propertyId, availableOnly: true);

        ViewData["Properties"] = properties;
        ViewData["SelectedPropertyId"] = propertyId;

        return View(units);
    }
}
