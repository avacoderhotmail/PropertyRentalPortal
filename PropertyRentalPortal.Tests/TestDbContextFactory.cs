using Microsoft.EntityFrameworkCore;
using PropertyRentalPortal.Web.Data;
using PropertyRentalPortal.Web.Data.Entities;
using Microsoft.Data.Sqlite;


namespace PropertyRentalPortal.Tests;

public static class TestDbContextFactory
{
    private static readonly Dictionary<string, SqliteConnection> _connections = new();

    public static ApplicationDbContext CreateInMemoryDbContext(string dbName)
    {
        // Reuse or create a persistent connection for this specific test name
        if (!_connections.TryGetValue(dbName, out var connection))
        {
            connection = new SqliteConnection($"DataSource={dbName};Mode=Memory;Cache=Shared");
            connection.Open();

            // Enable foreign keys once when the connection is first opened
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = OFF;";
            command.ExecuteNonQuery();

            _connections[dbName] = connection;
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new SqliteTestApplicationDbContext(options);

        // EnsureCreated() only needs to run once per database name
        context.Database.EnsureCreated();

        return context;
    }
    public static async Task<(Property property, UnitType activeType, UnitType inactiveType, Unit unit)> SeedBasicPropertyDataAsync(ApplicationDbContext context)
    {
        // Seed default test users so foreign keys and navigation includes resolve cleanly
        var applicant = new ApplicationUser
        {
            Id = "applicant-101",
            UserName = "applicant@test.com",
            Email = "applicant@test.com",
            FullName = "John Doe"
        };
        var manager = new ApplicationUser
        {
            Id = "manager-202",
            UserName = "manager@test.com",
            Email = "manager@test.com",
            FullName = "Sarah Manager"
        };
        context.Users.AddRange(applicant, manager);

        var activeType = new UnitType { Name = "1 Bedroom", IsActive = true };
        var inactiveType = new UnitType { Name = "Legacy Loft", IsActive = false };
        context.UnitTypes.AddRange(activeType, inactiveType);
        await context.SaveChangesAsync();

        var property = new Property
        {
            Name = "Parkside Apartments",
            Address = "100 Park Ave",
            City = "Chicago",
            State = "IL",
            ZipCode = "60601"
        };
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var unit = new Unit
        {
            PropertyId = property.Id,
            UnitNumber = "101",
            Bedrooms = 1,
            Bathrooms = 1.0m,
            MonthlyRent = 1500.00m,
            UnitTypeId = activeType.Id
        };
        context.Units.Add(unit);
        await context.SaveChangesAsync();

        return (property, activeType, inactiveType, unit);
    }
}
