using PropertyRentalPortal.Web.Data.Entities;
using PropertyRentalPortal.Web.Data.Enums;
using PropertyRentalPortal.Web.Models;
using PropertyRentalPortal.Web.Services;

namespace PropertyRentalPortal.Tests;

public class ApplicationServiceTests
{
    private const string ApplicantId = "applicant-101";
    private const string ManagerId = "manager-202";

    [Fact]
    public async Task StartApplication_OnUnavailableUnitWithActiveLease_Fails()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(StartApplication_OnUnavailableUnitWithActiveLease_Fails));
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);

        var today = DateTime.UtcNow.Date;
        context.Leases.Add(new Lease
        {
            UnitId = unit.Id,
            TenantUserId = "other-tenant",
            StartDate = today.AddMonths(-1),
            EndDate = today.AddMonths(11), // Active!
            MonthlyRent = unit.MonthlyRent
        });
        await context.SaveChangesAsync();

        var service = new ApplicationService(context);

        // Act
        var result = await service.StartApplicationAsync(unit.Id, ApplicantId, "John Doe", "john@example.com");

        // Assert: Requirement: "A unit whose lease term covers today is not available."
        Assert.False(result.Success);
        Assert.Contains("active lease", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartApplication_OnAvailableUnit_CreatesDraftApplication()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(StartApplication_OnAvailableUnit_CreatesDraftApplication));
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);
        var service = new ApplicationService(context);

        // Act
        var result = await service.StartApplicationAsync(unit.Id, ApplicantId, "John Doe", "john@example.com");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(ApplicationStatus.Draft, result.Data.Status);
        Assert.Equal(1, result.Data.CurrentSection);
    }

    [Fact]
    public async Task SaveApplicantInfo_ValidInput_PersistsAndAdvancesToSection2()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(SaveApplicantInfo_ValidInput_PersistsAndAdvancesToSection2));
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);
        var service = new ApplicationService(context);

        var startRes = await service.StartApplicationAsync(unit.Id, ApplicantId, "John Doe", "john@example.com");
        var appId = startRes.Data!.Id;

        var input = new ApplicantInfoInputModel
        {
            ApplicantFullName = "Johnathan Doe",
            ApplicantEmail = "johnathan@example.com",
            ApplicantPhone = "555-123-4567",
            CurrentAddress = "123 Main St",
            CurrentCity = "Chicago",
            CurrentState = "IL",
            CurrentZipCode = "60601"
        };

        // Act: Requirement 4b.i "Continue validates the current section, persists it only when it is valid, and shows the next section"
        var saveRes = await service.SaveApplicantInfoAsync(appId, ApplicantId, input);

        // Assert
        Assert.True(saveRes.Success);
        Assert.True(saveRes.Data!.ApplicantInfoSaved);
        Assert.Equal(2, saveRes.Data.CurrentSection);
        Assert.Equal("Johnathan Doe", saveRes.Data.ApplicantFullName);
    }

    [Fact]
    public async Task ValidateResidenceHistorySection_WithZeroResidences_Fails()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(ValidateResidenceHistorySection_WithZeroResidences_Fails));
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);
        var service = new ApplicationService(context);

        var startRes = await service.StartApplicationAsync(unit.Id, ApplicantId, "John Doe", "john@example.com");
        var appId = startRes.Data!.Id;

        // Act (No residences added yet)
        var result = await service.ValidateResidenceHistorySectionAsync(appId, ApplicantId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("at least one prior residence", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateResidenceHistorySection_WithResidenceAdded_SucceedsAndAdvancesToSummary()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(ValidateResidenceHistorySection_WithResidenceAdded_SucceedsAndAdvancesToSummary));
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);
        var service = new ApplicationService(context);

        var startRes = await service.StartApplicationAsync(unit.Id, ApplicantId, "John Doe", "john@example.com");
        var appId = startRes.Data!.Id;

        // Add a residence via modal service method
        await service.AddResidenceAsync(appId, ApplicantId, new ResidenceHistoryInputModel
        {
            RentalApplicationId = appId,
            Address = "456 Oak Ave",
            City = "Chicago",
            State = "IL",
            ZipCode = "60611",
            LandlordName = "Jane Landlady",
            LandlordPhone = "555-987-6543",
            MoveInDate = DateTime.UtcNow.AddYears(-2),
            MoveOutDate = DateTime.UtcNow.AddMonths(-2)
        });

        // Act
        var result = await service.ValidateResidenceHistorySectionAsync(appId, ApplicantId);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Data!.ResidenceHistorySaved);
        Assert.Equal(3, result.Data.CurrentSection); // Advanced to Summary
    }

    [Fact]
    public async Task SubmitApplication_WhenIncompleteSections_Fails()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(SubmitApplication_WhenIncompleteSections_Fails));
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);
        var service = new ApplicationService(context);

        var startRes = await service.StartApplicationAsync(unit.Id, ApplicantId, "John Doe", "john@example.com");
        var appId = startRes.Data!.Id;

        // Act: Attempt submit before sections are saved
        var result = await service.SubmitApplicationAsync(appId, ApplicantId);

        // Assert: Requirement: "Submit is available from the Summary only once both sections have been saved."
        Assert.False(result.Success);
        Assert.Contains("must be saved", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitApplication_WhenUnitHasActiveLease_RejectsDoubleLease()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(SubmitApplication_WhenUnitHasActiveLease_RejectsDoubleLease));
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);
        var service = new ApplicationService(context);

        var startRes = await service.StartApplicationAsync(unit.Id, ApplicantId, "John Doe", "john@example.com");
        var app = startRes.Data!;

        // Complete sections 1 and 2
        app.ApplicantInfoSaved = true;
        app.ResidenceHistorySaved = true;
        await context.SaveChangesAsync();

        // Simulate another lease taking effect before this applicant clicks Submit
        var today = DateTime.UtcNow.Date;
        context.Leases.Add(new Lease
        {
            UnitId = unit.Id,
            TenantUserId = "prior-applicant",
            StartDate = today,
            EndDate = today.AddYears(1).AddDays(-1),
            MonthlyRent = unit.MonthlyRent
        });
        await context.SaveChangesAsync();

        // Act: Requirement 4e: "At submit and again at approval, reject the action with an error when the unit has an active lease."
        var submitRes = await service.SubmitApplicationAsync(app.Id, ApplicantId);

        // Assert
        Assert.False(submitRes.Success);
        Assert.Contains("active lease", submitRes.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitApplication_WhenValid_TransitionsToSubmitted()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(SubmitApplication_WhenValid_TransitionsToSubmitted));
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);
        var service = new ApplicationService(context);

        var startRes = await service.StartApplicationAsync(unit.Id, ApplicantId, "John Doe", "john@example.com");
        var app = startRes.Data!;
        app.ApplicantInfoSaved = true;
        app.ResidenceHistorySaved = true;
        await context.SaveChangesAsync();

        // Act
        var submitRes = await service.SubmitApplicationAsync(app.Id, ApplicantId);

        // Assert
        Assert.True(submitRes.Success);
        Assert.Equal(ApplicationStatus.Submitted, submitRes.Data!.Status);
        Assert.NotNull(submitRes.Data.SubmittedAt);
    }

    [Fact]
    public async Task ReviewApplication_Approve_WhenUnitHasActiveLease_RejectsDoubleLease()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(ReviewApplication_Approve_WhenUnitHasActiveLease_RejectsDoubleLease));
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);
        var service = new ApplicationService(context);

        var startRes = await service.StartApplicationAsync(unit.Id, ApplicantId, "John Doe", "john@example.com");
        var app = startRes.Data!;
        app.ApplicantInfoSaved = true;
        app.ResidenceHistorySaved = true;
        app.Status = ApplicationStatus.Submitted;
        await context.SaveChangesAsync();

        // Simulate active lease already existing on the unit
        var today = DateTime.UtcNow.Date;
        context.Leases.Add(new Lease
        {
            UnitId = unit.Id,
            TenantUserId = "tenant-xyz",
            StartDate = today.AddDays(-10),
            EndDate = today.AddYears(1),
            MonthlyRent = unit.MonthlyRent
        });
        await context.SaveChangesAsync();

        // Act: Requirement: "The approval check prevents a second lease."
        var reviewRes = await service.ReviewApplicationAsync(app.Id, ManagerId, ReviewOutcome.Approve, "Looks good");

        // Assert
        Assert.False(reviewRes.Success);
        Assert.Contains("active lease", reviewRes.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReviewApplication_Approve_WhenAvailable_IssuesTwelveMonthLeaseAndApproves()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(ReviewApplication_Approve_WhenAvailable_IssuesTwelveMonthLeaseAndApproves));
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);
        var service = new ApplicationService(context);

        var startRes = await service.StartApplicationAsync(unit.Id, ApplicantId, "John Doe", "john@example.com");
        var app = startRes.Data!;
        app.ApplicantInfoSaved = true;
        app.ResidenceHistorySaved = true;
        app.Status = ApplicationStatus.Submitted;
        await context.SaveChangesAsync();

        // Act: Requirement 1b: "An approved application issues a twelve-month lease for the unit."
        var reviewRes = await service.ReviewApplicationAsync(app.Id, ManagerId, ReviewOutcome.Approve, "Background check cleared.");

        // Assert
        Assert.True(reviewRes.Success);
        Assert.Equal(ApplicationStatus.Approved, reviewRes.Data!.Status);
        Assert.NotNull(reviewRes.Data.Lease);
        Assert.Equal(unit.Id, reviewRes.Data.Lease.UnitId);
        Assert.Equal(ApplicantId, reviewRes.Data.Lease.TenantUserId);

        // Verify 12-month term
        var today = DateTime.UtcNow.Date;
        Assert.Equal(today, reviewRes.Data.Lease.StartDate.Date);
        Assert.Equal(today.AddYears(1).AddDays(-1), reviewRes.Data.Lease.EndDate.Date);
    }

    [Fact]
    public async Task ReviewApplication_Return_WithoutComment_FailsRequirement()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(ReviewApplication_Return_WithoutComment_FailsRequirement));
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);
        var service = new ApplicationService(context);

        var startRes = await service.StartApplicationAsync(unit.Id, ApplicantId, "John Doe", "john@example.com");
        var app = startRes.Data!;
        app.Status = ApplicationStatus.Submitted;
        await context.SaveChangesAsync();

        // Act: Requirement: "comment (required for Return and Deny)"
        var reviewRes = await service.ReviewApplicationAsync(app.Id, ManagerId, ReviewOutcome.Return, comment: null);

        // Assert
        Assert.False(reviewRes.Success);
        Assert.Contains("comment is required", reviewRes.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReviewApplication_Deny_WithoutComment_FailsRequirement()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(ReviewApplication_Deny_WithoutComment_FailsRequirement));
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);
        var service = new ApplicationService(context);

        var startRes = await service.StartApplicationAsync(unit.Id, ApplicantId, "John Doe", "john@example.com");
        var app = startRes.Data!;
        app.Status = ApplicationStatus.Submitted;
        await context.SaveChangesAsync();

        // Act: Requirement: "comment (required for Return and Deny)"
        var reviewRes = await service.ReviewApplicationAsync(app.Id, ManagerId, ReviewOutcome.Deny, comment: "   ");

        // Assert
        Assert.False(reviewRes.Success);
        Assert.Contains("comment is required", reviewRes.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReviewApplication_Return_WithComment_TransitionsToReturned()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(ReviewApplication_Return_WithComment_TransitionsToReturned));
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);
        var service = new ApplicationService(context);

        var startRes = await service.StartApplicationAsync(unit.Id, ApplicantId, "John Doe", "john@example.com");
        var app = startRes.Data!;
        app.Status = ApplicationStatus.Submitted;
        await context.SaveChangesAsync();

        // Act
        var reviewRes = await service.ReviewApplicationAsync(app.Id, ManagerId, ReviewOutcome.Return, "Missing landlord phone number.");

        // Assert
        Assert.True(reviewRes.Success);
        Assert.Equal(ApplicationStatus.Returned, reviewRes.Data!.Status);
        Assert.True(reviewRes.Data.IsEditable); // Requirement: "An applicant can edit while the application is in a Draft or Returned status"
    }

    [Fact]
    public async Task WithdrawApplication_TerminalStatus_Fails()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(WithdrawApplication_TerminalStatus_Fails));
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);
        var service = new ApplicationService(context);

        var startRes = await service.StartApplicationAsync(unit.Id, ApplicantId, "John Doe", "john@example.com");
        var app = startRes.Data!;
        app.Status = ApplicationStatus.Approved; // Terminal!
        await context.SaveChangesAsync();

        // Act: Requirement: "Approved, Denied, and Withdrawn are terminal."
        var withdrawRes = await service.WithdrawApplicationAsync(app.Id, ApplicantId, "Changed my mind");

        // Assert
        Assert.False(withdrawRes.Success);
        Assert.Contains("terminal", withdrawRes.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClaimAndRelease_ReviewQueue_TransitionsCorrectly()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext(nameof(ClaimAndRelease_ReviewQueue_TransitionsCorrectly));
        var (_, _, _, unit) = await TestDbContextFactory.SeedBasicPropertyDataAsync(context);
        var service = new ApplicationService(context);

        var startRes = await service.StartApplicationAsync(unit.Id, ApplicantId, "John Doe", "john@example.com");
        var app = startRes.Data!;
        app.Status = ApplicationStatus.Submitted;
        await context.SaveChangesAsync();

        // Act 1: Claim
        var claimRes = await service.ClaimApplicationAsync(app.Id, ManagerId);
        Assert.True(claimRes.Success);
        var claimedApp = await service.GetApplicationByIdAsync(app.Id, isManager: true);
        Assert.Equal(ApplicationStatus.UnderReview, claimedApp!.Status);
        Assert.Equal(ManagerId, claimedApp.ClaimedByUserId);

        // Act 2: Release
        var releaseRes = await service.ReleaseApplicationAsync(app.Id, ManagerId);
        Assert.True(releaseRes.Success);
        var releasedApp = await service.GetApplicationByIdAsync(app.Id, isManager: true);
        Assert.Equal(ApplicationStatus.Submitted, releasedApp!.Status);
        Assert.Null(releasedApp.ClaimedByUserId);
    }
}
