using Microsoft.EntityFrameworkCore;
using PropertyRentalPortal.Web.Data.Entities;
using PropertyRentalPortal.Web.Data.Enums;
using PropertyRentalPortal.Web.Services;

namespace PropertyRentalPortal.Tests;

public class ApplicationConcurrencyTests
{
    private const string Applicant1Id = "applicant-101";
    private const string Applicant2Id = "applicant-102";
    private const string Manager1Id = "manager-201";
    private const string Manager2Id = "manager-202";

    [Fact]
    public async Task ReviewApplication_ConcurrentApproveByTwoManagers_OnlyFirstSucceedsAndIssuesSingleLease()
    {
        // Arrange: Setup database with a unit and submitted application
        var dbName = nameof(ReviewApplication_ConcurrentApproveByTwoManagers_OnlyFirstSucceedsAndIssuesSingleLease);
        using var setupContext = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(setupContext);

        var setupService = new ApplicationService(setupContext);
        var startRes = await setupService.StartApplicationAsync(unit.Id, Applicant1Id, "Alice Tenant", "alice@test.com");
        var app = startRes.Data!;
        app.ApplicantInfoSaved = true;
        app.ResidenceHistorySaved = true;
        app.Status = ApplicationStatus.Submitted;
        await setupContext.SaveChangesAsync();

        var appId = app.Id;

        // Act 1: Manager 1 reviews and approves the application
        using var contextManager1 = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var service1 = new ApplicationService(contextManager1);
        var reviewRes1 = await service1.ReviewApplicationAsync(appId, Manager1Id, ReviewOutcome.Approve, "Approved by Manager 1");

        // Act 2: Manager 2 attempts to approve the same application concurrently
        using var contextManager2 = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var service2 = new ApplicationService(contextManager2);
        var reviewRes2 = await service2.ReviewApplicationAsync(appId, Manager2Id, ReviewOutcome.Approve, "Approved by Manager 2");

        // Assert:
        // Manager 1's approval succeeded and issued the lease
        Assert.True(reviewRes1.Success);
        Assert.Equal(ApplicationStatus.Approved, reviewRes1.Data!.Status);
        Assert.NotNull(reviewRes1.Data.Lease);

        // Manager 2's approval must be rejected because the application is already approved (terminal)
        Assert.False(reviewRes2.Success);
        Assert.Contains("Only submitted or under review applications can be reviewed", reviewRes2.ErrorMessage);

        // Verify database state: Exactly one lease exists for the unit and application
        using var verifyContext = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var leases = await verifyContext.Leases.Where(l => l.UnitId == unit.Id).ToListAsync();
        Assert.Single(leases);
        Assert.Equal(Applicant1Id, leases[0].TenantUserId);
        Assert.Equal(appId, leases[0].ApplicationId);

        var finalApp = await verifyContext.RentalApplications.FindAsync(appId);
        Assert.NotNull(finalApp);
        Assert.Equal(ApplicationStatus.Approved, finalApp.Status);
    }

    [Fact]
    public async Task ReviewApplication_ConcurrentApproveAndDeny_Manager1ApproveWins_Manager2DenyRejected()
    {
        // Arrange
        var dbName = nameof(ReviewApplication_ConcurrentApproveAndDeny_Manager1ApproveWins_Manager2DenyRejected);
        using var setupContext = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(setupContext);

        var setupService = new ApplicationService(setupContext);
        var startRes = await setupService.StartApplicationAsync(unit.Id, Applicant1Id, "Alice Tenant", "alice@test.com");
        var app = startRes.Data!;
        app.ApplicantInfoSaved = true;
        app.ResidenceHistorySaved = true;
        app.Status = ApplicationStatus.Submitted;
        await setupContext.SaveChangesAsync();

        var appId = app.Id;

        // Act 1: Manager 1 approves and issues a lease
        using var contextManager1 = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var service1 = new ApplicationService(contextManager1);
        var approveRes = await service1.ReviewApplicationAsync(appId, Manager1Id, ReviewOutcome.Approve, "Approved by Manager 1");

        // Act 2: Manager 2 attempts to deny the application concurrently
        using var contextManager2 = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var service2 = new ApplicationService(contextManager2);
        var denyRes = await service2.ReviewApplicationAsync(appId, Manager2Id, ReviewOutcome.Deny, "Credit check failed");

        // Assert:
        Assert.True(approveRes.Success);
        Assert.False(denyRes.Success);
        Assert.Contains("Only submitted or under review applications can be reviewed", denyRes.ErrorMessage);

        // Verify database state: Application remains Approved with its lease, not Denied
        using var verifyContext = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var finalApp = await verifyContext.RentalApplications.Include(a => a.Lease).FirstOrDefaultAsync(a => a.Id == appId);
        Assert.NotNull(finalApp);
        Assert.Equal(ApplicationStatus.Approved, finalApp.Status);
        Assert.NotNull(finalApp.Lease);
    }

    [Fact]
    public async Task ReviewApplication_TwoManagersApproveDifferentApplicationsForSameUnit_PreventsDoubleLease()
    {
        // Arrange: 1 Unit with 2 different applicants having submitted applications
        var dbName = nameof(ReviewApplication_TwoManagersApproveDifferentApplicationsForSameUnit_PreventsDoubleLease);
        using var setupContext = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(setupContext);

        // Add second applicant
        setupContext.Users.Add(new ApplicationUser
        {
            Id = Applicant2Id,
            UserName = "bob@test.com",
            Email = "bob@test.com",
            FullName = "Bob Tenant"
        });
        await setupContext.SaveChangesAsync();

        var setupService = new ApplicationService(setupContext);

        // Application 1
        var start1 = await setupService.StartApplicationAsync(unit.Id, Applicant1Id, "Alice Tenant", "alice@test.com");
        var app1 = start1.Data!;
        app1.ApplicantInfoSaved = true;
        app1.ResidenceHistorySaved = true;
        app1.Status = ApplicationStatus.Submitted;

        // Application 2 for the exact same unit
        var start2 = await setupService.StartApplicationAsync(unit.Id, Applicant2Id, "Bob Tenant", "bob@test.com");
        var app2 = start2.Data!;
        app2.ApplicantInfoSaved = true;
        app2.ResidenceHistorySaved = true;
        app2.Status = ApplicationStatus.Submitted;

        await setupContext.SaveChangesAsync();

        // Act 1: Manager 1 approves Application 1 -> creates a 12-month lease for the unit
        using var contextManager1 = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var service1 = new ApplicationService(contextManager1);
        var approve1 = await service1.ReviewApplicationAsync(app1.Id, Manager1Id, ReviewOutcome.Approve, "Approved Alice");

        // Act 2: Manager 2 attempts to approve Application 2 for the same unit
        using var contextManager2 = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var service2 = new ApplicationService(contextManager2);
        var approve2 = await service2.ReviewApplicationAsync(app2.Id, Manager2Id, ReviewOutcome.Approve, "Approved Bob");

        // Assert:
        Assert.True(approve1.Success);

        // Second approval must fail because the unit is already leased
        Assert.False(approve2.Success);
        Assert.Contains("active lease", approve2.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Verify only 1 lease was created in the database for the unit
        using var verifyContext = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var leases = await verifyContext.Leases.Where(l => l.UnitId == unit.Id).ToListAsync();
        Assert.Single(leases);
        Assert.Equal(Applicant1Id, leases[0].TenantUserId);
        Assert.Equal(app1.Id, leases[0].ApplicationId);

        // Application 2 must not be approved
        var finalApp2 = await verifyContext.RentalApplications.FindAsync(app2.Id);
        Assert.NotNull(finalApp2);
        Assert.Equal(ApplicationStatus.Submitted, finalApp2.Status);
    }

    [Fact]
    public async Task ClaimApplication_ConcurrentClaimByTwoManagers_OnlyFirstClaimWins()
    {
        // Arrange
        var dbName = nameof(ClaimApplication_ConcurrentClaimByTwoManagers_OnlyFirstClaimWins);
        using var setupContext = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(setupContext);

        var setupService = new ApplicationService(setupContext);
        var startRes = await setupService.StartApplicationAsync(unit.Id, Applicant1Id, "Alice Tenant", "alice@test.com");
        var app = startRes.Data!;
        app.Status = ApplicationStatus.Submitted;
        await setupContext.SaveChangesAsync();

        var appId = app.Id;

        // Act 1: Manager 1 claims the application
        using var contextManager1 = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var service1 = new ApplicationService(contextManager1);
        var claimRes1 = await service1.ClaimApplicationAsync(appId, Manager1Id);

        // Act 2: Manager 2 attempts to claim the same application concurrently
        using var contextManager2 = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var service2 = new ApplicationService(contextManager2);
        var claimRes2 = await service2.ClaimApplicationAsync(appId, Manager2Id);

        // Assert:
        Assert.True(claimRes1.Success);
        Assert.False(claimRes2.Success);
        Assert.Contains("Only submitted applications can be claimed", claimRes2.ErrorMessage);

        // Verify application is claimed by Manager 1, not Manager 2
        using var verifyContext = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var finalApp = await verifyContext.RentalApplications.FindAsync(appId);
        Assert.NotNull(finalApp);
        Assert.Equal(ApplicationStatus.UnderReview, finalApp.Status);
        Assert.Equal(Manager1Id, finalApp.ClaimedByUserId);
    }

    [Fact]
    public async Task ReviewApplication_OptimisticConcurrency_ThrowsDbUpdateConcurrencyException_WhenRowVersionStale()
    {
        // Arrange: Setup entity with initial RowVersion
        var dbName = nameof(ReviewApplication_OptimisticConcurrency_ThrowsDbUpdateConcurrencyException_WhenRowVersionStale);
        using var setupContext = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(setupContext);

        var app = new RentalApplication
        {
            UnitId = unit.Id,
            ApplicantUserId = Applicant1Id,
            ApplicantFullName = "Alice Tenant",
            ApplicantEmail = "alice@test.com",
            ApplicantPhone = "555-1234",
            CurrentAddress = "123 Main St",
            Status = ApplicationStatus.Submitted,
            RowVersion = new byte[] { 0x01 }
        };
        setupContext.RentalApplications.Add(app);
        await setupContext.SaveChangesAsync();

        var appId = app.Id;

        // Act: Context 1 updates the application and simulates a RowVersion change (as SQL Server rowversion does)
        using var context1 = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var appFrom1 = await context1.RentalApplications.FindAsync(appId);
        Assert.NotNull(appFrom1);
        appFrom1.ApplicantFullName = "Alice Concurrency Updated";
        appFrom1.RowVersion = new byte[] { 0x02 }; // Simulates rowversion change in database
        await context1.SaveChangesAsync();

        // Context 2 has the original stale entity (original RowVersion = 0x01)
        using var context2 = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        var appFrom2 = new RentalApplication
        {
            Id = appId,
            UnitId = unit.Id,
            ApplicantUserId = Applicant1Id,
            ApplicantFullName = "Alice Stale",
            ApplicantEmail = "alice@test.com",
            ApplicantPhone = "555-1234",
            CurrentAddress = "123 Main St",
            Status = ApplicationStatus.Denied,
            RowVersion = new byte[] { 0x02 } // What it wants to set
        };

        // Attach with original RowVersion 0x01
        context2.RentalApplications.Attach(appFrom2);
        context2.Entry(appFrom2).Property(a => a.Status).IsModified = true;
        context2.Entry(appFrom2).Property(a => a.RowVersion).OriginalValue = new byte[] { 0x01 }; // Stale token!

        // Assert: EF Core change tracker identifies that the original value does not match current store value
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context2.SaveChangesAsync());
    }
}
