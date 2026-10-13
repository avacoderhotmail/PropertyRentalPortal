using Microsoft.EntityFrameworkCore;
using PropertyRentalPortal.Web.Data;
using PropertyRentalPortal.Web.Data.Entities;
using PropertyRentalPortal.Web.Services.Common;

namespace PropertyRentalPortal.Web.Services;

public class UnitService : IUnitService
{
    private readonly ApplicationDbContext _context;

    public UnitService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Property>> GetPropertiesAsync()
    {
        return await _context.Properties
            .Include(p => p.Units)
                .ThenInclude(u => u.UnitType)
            .Include(p => p.Units)
                .ThenInclude(u => u.Leases)
            .OrderBy(p => p.Name)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<Property?> GetPropertyByIdAsync(int id)
    {
        return await _context.Properties
            .Include(p => p.Units)
                .ThenInclude(u => u.UnitType)
            .Include(p => p.Units)
                .ThenInclude(u => u.Leases)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<ServiceResult<Property>> CreatePropertyAsync(Property property)
    {
        if (string.IsNullOrWhiteSpace(property.Name))
            return ServiceResult<Property>.Fail("Property name is required.");

        _context.Properties.Add(property);
        await _context.SaveChangesAsync();
        return ServiceResult<Property>.Ok(property);
    }

    public async Task<ServiceResult<Property>> UpdatePropertyAsync(Property property)
    {
        var existing = await _context.Properties.FindAsync(property.Id);
        if (existing == null)
            return ServiceResult<Property>.Fail("Property not found.");

        existing.Name = property.Name;
        existing.Address = property.Address;
        existing.City = property.City;
        existing.State = property.State;
        existing.ZipCode = property.ZipCode;
        existing.Description = property.Description;

        await _context.SaveChangesAsync();
        return ServiceResult<Property>.Ok(existing);
    }

    public async Task<ServiceResult> DeletePropertyAsync(int propertyId)
    {
        var property = await _context.Properties
            .Include(p => p.Units)
                .ThenInclude(u => u.Leases)
            .Include(p => p.Units)
                .ThenInclude(u => u.Applications)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == propertyId);

        if (property == null)
            return ServiceResult.Fail("Property not found.");

        var today = DateTime.UtcNow.Date;
        var hasActiveLeases = property.Units.Any(u => u.Leases.Any(l => l.StartDate.Date <= today && today <= l.EndDate.Date));
        if (hasActiveLeases)
            return ServiceResult.Fail("Cannot delete property that has units with active leases.");

        _context.Properties.Remove(property);
        await _context.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<List<Unit>> GetUnitsAsync(int? propertyId = null, bool availableOnly = false)
    {
        var query = _context.Units
            .Include(u => u.Property)
            .Include(u => u.UnitType)
            .Include(u => u.Leases)
            .AsSplitQuery()
            .AsQueryable();

        if (propertyId.HasValue)
        {
            query = query.Where(u => u.PropertyId == propertyId.Value);
        }

        var units = await query.OrderBy(u => u.Property.Name).ThenBy(u => u.UnitNumber).ToListAsync();

        if (availableOnly)
        {
            var today = DateTime.UtcNow.Date;
            units = units.Where(u => !u.Leases.Any(l => l.StartDate.Date <= today && today <= l.EndDate.Date)).ToList();
        }

        return units;
    }

    public async Task<Unit?> GetUnitByIdAsync(int id)
    {
        return await _context.Units
            .Include(u => u.Property)
            .Include(u => u.UnitType)
            .Include(u => u.Leases)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    /// <summary>
    /// Requirement: "Unit Type is a lookup with Active and Inactive values.
    /// An inactive value still displays on a unit that already uses it but cannot be selected for any other unit.
    /// Enforce this on the server."
    /// </summary>
    public async Task<List<UnitType>> GetUnitTypesForSelectionAsync(int? currentUnitTypeId = null)
    {
        return await _context.UnitTypes
            .Where(ut => ut.IsActive || (currentUnitTypeId.HasValue && ut.Id == currentUnitTypeId.Value))
            .OrderBy(ut => ut.Name)
            .ToListAsync();
    }

    public async Task<ServiceResult<Unit>> CreateUnitAsync(Unit unit)
    {
        // Server-side enforcement: new units can only select active unit types
        var unitType = await _context.UnitTypes.FindAsync(unit.UnitTypeId);
        if (unitType == null)
            return ServiceResult<Unit>.Fail("Specified unit type does not exist.");

        if (!unitType.IsActive)
            return ServiceResult<Unit>.Fail($"Unit Type '{unitType.Name}' is inactive and cannot be assigned to new units.");

        // Check unique unit number per property
        var duplicate = await _context.Units.AnyAsync(u => u.PropertyId == unit.PropertyId && u.UnitNumber == unit.UnitNumber);
        if (duplicate)
            return ServiceResult<Unit>.Fail($"Unit number '{unit.UnitNumber}' already exists for this property.");

        _context.Units.Add(unit);
        await _context.SaveChangesAsync();
        return ServiceResult<Unit>.Ok(unit);
    }

    public async Task<ServiceResult<Unit>> UpdateUnitAsync(Unit unit)
    {
        var existing = await _context.Units.FindAsync(unit.Id);
        if (existing == null)
            return ServiceResult<Unit>.Fail("Unit not found.");

        // Server-side enforcement: if changing unit type, the new type MUST be active
        if (existing.UnitTypeId != unit.UnitTypeId)
        {
            var newUnitType = await _context.UnitTypes.FindAsync(unit.UnitTypeId);
            if (newUnitType == null)
                return ServiceResult<Unit>.Fail("Specified unit type does not exist.");

            if (!newUnitType.IsActive)
                return ServiceResult<Unit>.Fail($"Unit Type '{newUnitType.Name}' is inactive and cannot be selected.");
        }

        // Check duplicate unit number if changed
        if (existing.UnitNumber != unit.UnitNumber || existing.PropertyId != unit.PropertyId)
        {
            var duplicate = await _context.Units.AnyAsync(u => u.PropertyId == unit.PropertyId && u.UnitNumber == unit.UnitNumber && u.Id != unit.Id);
            if (duplicate)
                return ServiceResult<Unit>.Fail($"Unit number '{unit.UnitNumber}' already exists for this property.");
        }

        existing.PropertyId = unit.PropertyId;
        existing.UnitNumber = unit.UnitNumber;
        existing.Bedrooms = unit.Bedrooms;
        existing.Bathrooms = unit.Bathrooms;
        existing.MonthlyRent = unit.MonthlyRent;
        existing.UnitTypeId = unit.UnitTypeId;

        await _context.SaveChangesAsync();
        return ServiceResult<Unit>.Ok(existing);
    }

    public async Task<ServiceResult> DeleteUnitAsync(int unitId)
    {
        var unit = await _context.Units
            .Include(u => u.Leases)
            .Include(u => u.Applications)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Id == unitId);

        if (unit == null)
            return ServiceResult.Fail("Unit not found.");

        var today = DateTime.UtcNow.Date;
        var hasActiveLease = unit.Leases.Any(l => l.StartDate.Date <= today && today <= l.EndDate.Date);
        if (hasActiveLease)
            return ServiceResult.Fail("Cannot delete a unit that currently has an active lease.");

        _context.Units.Remove(unit);
        await _context.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<bool> IsUnitAvailableAsync(int unitId, DateTime? asOfDate = null)
    {
        var targetDate = (asOfDate ?? DateTime.UtcNow).Date;
        var hasActiveLease = await _context.Leases
            .AnyAsync(l => l.UnitId == unitId && l.StartDate.Date <= targetDate && targetDate <= l.EndDate.Date);

        return !hasActiveLease;
    }
}
