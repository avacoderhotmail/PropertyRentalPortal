using PropertyRentalPortal.Web.Data.Entities;
using PropertyRentalPortal.Web.Services;

namespace PropertyRentalPortal.Tests;

public class UnitServiceTests
{
    [Fact]
    public async Task CreateUnit_WithActiveUnitType_Succeeds()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(CreateUnit_WithActiveUnitType_Succeeds));
        var (property, activeType, _, _) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);
        var service = new UnitService(context);

        var newUnit = new Unit
        {
            PropertyId = property.Id,
            UnitNumber = "102",
            Bedrooms = 2,
            Bathrooms = 1.5m,
            MonthlyRent = 1750m,
            UnitTypeId = activeType.Id
        };

        // Act
        var result = await service.CreateUnitAsync(newUnit);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("102", result.Data.UnitNumber);
    }

    [Fact]
    public async Task CreateUnit_WithInactiveUnitType_FailsServerEnforcement()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(CreateUnit_WithInactiveUnitType_FailsServerEnforcement));
        var (property, _, inactiveType, _) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);
        var service = new UnitService(context);

        var newUnit = new Unit
        {
            PropertyId = property.Id,
            UnitNumber = "103",
            Bedrooms = 1,
            Bathrooms = 1.0m,
            MonthlyRent = 1600m,
            UnitTypeId = inactiveType.Id // Inactive!
        };

        // Act
        var result = await service.CreateUnitAsync(newUnit);

        // Assert: Requirement: "An inactive value... cannot be selected for any other unit. Enforce this on the server."
        Assert.False(result.Success);
        Assert.Contains("inactive", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateUnit_ChangingToInactiveUnitType_FailsServerEnforcement()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(UpdateUnit_ChangingToInactiveUnitType_FailsServerEnforcement));
        var (_, _, inactiveType, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);
        var service = new UnitService(context);

        var updateUnit = new Unit
        {
            Id = unit.Id,
            PropertyId = unit.PropertyId,
            UnitNumber = unit.UnitNumber,
            Bedrooms = unit.Bedrooms,
            Bathrooms = unit.Bathrooms,
            MonthlyRent = unit.MonthlyRent,
            UnitTypeId = inactiveType.Id // Attempting to change to inactive type
        };

        // Act
        var result = await service.UpdateUnitAsync(updateUnit);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("inactive", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateUnit_RetainingExistingInactiveUnitType_Succeeds()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(UpdateUnit_RetainingExistingInactiveUnitType_Succeeds));
        var (property, _, inactiveType, _) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);

        // Create a unit that already uses the inactive type (e.g. grandfathered in)
        var existingUnitWithInactiveType = new Unit
        {
            PropertyId = property.Id,
            UnitNumber = "Legacy-1",
            Bedrooms = 1,
            Bathrooms = 1.0m,
            MonthlyRent = 1400m,
            UnitTypeId = inactiveType.Id
        };
        context.Units.Add(existingUnitWithInactiveType);
        await context.SaveChangesAsync();

        var service = new UnitService(context);

        // Update rent while keeping existing inactive unit type
        var updateModel = new Unit
        {
            Id = existingUnitWithInactiveType.Id,
            PropertyId = existingUnitWithInactiveType.PropertyId,
            UnitNumber = existingUnitWithInactiveType.UnitNumber,
            Bedrooms = existingUnitWithInactiveType.Bedrooms,
            Bathrooms = existingUnitWithInactiveType.Bathrooms,
            MonthlyRent = 1550m, // updated rent
            UnitTypeId = inactiveType.Id // Kept the same inactive type
        };

        // Act: Requirement: "An inactive value still displays on a unit that already uses it"
        var result = await service.UpdateUnitAsync(updateModel);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1550m, result.Data!.MonthlyRent);
    }

    [Fact]
    public async Task IsUnitAvailable_ActiveLeaseCoveringToday_ReturnsFalse()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(IsUnitAvailable_ActiveLeaseCoveringToday_ReturnsFalse));
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);

        var today = DateTime.UtcNow.Date;
        var activeLease = new Lease
        {
            UnitId = unit.Id,
            TenantUserId = "user-123",
            StartDate = today.AddMonths(-3),
            EndDate = today.AddMonths(9), // Covers today!
            MonthlyRent = unit.MonthlyRent
        };
        context.Leases.Add(activeLease);
        await context.SaveChangesAsync();

        var service = new UnitService(context);

        // Act: Requirement: "A unit whose lease term covers today is not available."
        var isAvailable = await service.IsUnitAvailableAsync(unit.Id);

        // Assert
        Assert.False(isAvailable);
    }

    [Fact]
    public async Task IsUnitAvailable_ExpiredPastLease_ReturnsTrue()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(IsUnitAvailable_ExpiredPastLease_ReturnsTrue));
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);

        var today = DateTime.UtcNow.Date;
        var pastLease = new Lease
        {
            UnitId = unit.Id,
            TenantUserId = "user-123",
            StartDate = today.AddYears(-2),
            EndDate = today.AddDays(-1), // Expired yesterday
            MonthlyRent = unit.MonthlyRent
        };
        context.Leases.Add(pastLease);
        await context.SaveChangesAsync();

        var service = new UnitService(context);

        // Act
        var isAvailable = await service.IsUnitAvailableAsync(unit.Id);

        // Assert
        Assert.True(isAvailable);
    }

    [Fact]
    public async Task UpdateUnit_WithStaleRowVersion_ReturnsConcurrencyConflict()
    {
        // Arrange
        var dbName = nameof(UpdateUnit_WithStaleRowVersion_ReturnsConcurrencyConflict);
        using var setupContext = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(setupContext);

        unit.RowVersion = new byte[] { 0x01 };
        await setupContext.SaveChangesAsync();
        var unitId = unit.Id;

        // Context 1 simulates concurrent update by Manager 1 (updating rent to $1900, RowVersion to 0x02)
        using var context1 = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var unitFrom1 = await context1.Units.FindAsync(unitId);
        Assert.NotNull(unitFrom1);
        unitFrom1.MonthlyRent = 1900m;
        unitFrom1.RowVersion = new byte[] { 0x02 };
        await context1.SaveChangesAsync();

        // Context 2 (Manager 2) attempts to update the unit with stale RowVersion 0x01
        using var context2 = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var service2 = new UnitService(context2);
        var unitFrom2 = new Unit
        {
            Id = unitId,
            PropertyId = unit.PropertyId,
            UnitNumber = unit.UnitNumber,
            Bedrooms = unit.Bedrooms,
            Bathrooms = unit.Bathrooms,
            MonthlyRent = 2000m, // Manager 2 wanted $2000
            UnitTypeId = unit.UnitTypeId,
            RowVersion = new byte[] { 0x01 } // Stale token!
        };

        // Act
        var result = await service2.UpdateUnitAsync(unitFrom2, rowVersion: new byte[] { 0x01 });

        // Assert
        Assert.False(result.Success);
        Assert.True(result.IsConcurrencyConflict);
        Assert.Contains("modified by another user", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Verify database retained Manager 1's $1900 rent, not Manager 2's $2000
        using var verifyContext = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var finalUnit = await verifyContext.Units.FindAsync(unitId);
        Assert.NotNull(finalUnit);
        Assert.Equal(1900m, finalUnit.MonthlyRent);
    }
}
