using PropertyRentalPortal.Web.Data.Entities;
using PropertyRentalPortal.Web.Services.Common;

namespace PropertyRentalPortal.Web.Services;

public interface IUnitService
{
    Task<List<Property>> GetPropertiesAsync();
    Task<Property?> GetPropertyByIdAsync(int id);
    Task<ServiceResult<Property>> CreatePropertyAsync(Property property);
    Task<ServiceResult<Property>> UpdatePropertyAsync(Property property);
    Task<ServiceResult> DeletePropertyAsync(int propertyId);

    Task<List<Unit>> GetUnitsAsync(int? propertyId = null, bool availableOnly = false);
    Task<Unit?> GetUnitByIdAsync(int id);
    Task<List<UnitType>> GetUnitTypesForSelectionAsync(int? currentUnitTypeId = null);
    Task<ServiceResult<Unit>> CreateUnitAsync(Unit unit);
    Task<ServiceResult<Unit>> UpdateUnitAsync(Unit unit);
    Task<ServiceResult> DeleteUnitAsync(int unitId);

    Task<bool> IsUnitAvailableAsync(int unitId, DateTime? asOfDate = null);
}
