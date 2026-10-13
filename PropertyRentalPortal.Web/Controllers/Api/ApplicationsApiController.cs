using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyRentalPortal.Web.Data.Enums;
using PropertyRentalPortal.Web.Data.Seeding;
using PropertyRentalPortal.Web.Models;
using PropertyRentalPortal.Web.Services;

namespace PropertyRentalPortal.Web.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class ApplicationsApiController : ControllerBase
{
    private readonly IApplicationService _applicationService;

    public ApplicationsApiController(IApplicationService applicationService)
    {
        _applicationService = applicationService;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private bool IsPropertyManager => User.IsInRole(DatabaseSeeder.PropertyManagerRole);

    /// <summary>
    /// Fetches a paged, sorted, and filtered list of rental applications from the database.
    /// Documented with OpenAPI.
    /// </summary>
    /// <param name="status">Filter by application status</param>
    /// <param name="propertyId">Filter by property ID</param>
    /// <param name="sortBy">Column to sort by (property, applicantname, status, createdat, submittedat)</param>
    /// <param name="sortDescending">Sort direction</param>
    /// <param name="page">Page number (1-indexed)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paged grid result with items and total count</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedGridResult<ApplicationListItemViewModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedGridResult<ApplicationListItemViewModel>>> GetApplications(
        [FromQuery] ApplicationStatus? status = null,
        [FromQuery] int? propertyId = null,
        [FromQuery] string? sortBy = "CreatedAt",
        [FromQuery] bool sortDescending = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = pageSize <= 0 ? 10 : Math.Min(100, pageSize);

        var apps = await _applicationService.GetApplicationsListAsync(
            CurrentUserId,
            IsPropertyManager,
            status,
            propertyId,
            sortBy,
            sortDescending,
            page,
            pageSize);

        var totalCount = await _applicationService.GetApplicationsCountAsync(
            CurrentUserId,
            IsPropertyManager,
            status,
            propertyId);

        var items = apps.Select(a => new ApplicationListItemViewModel
        {
            Id = a.Id,
            UnitId = a.UnitId,
            UnitNumber = a.Unit?.UnitNumber ?? string.Empty,
            PropertyName = a.Unit?.Property?.Name ?? string.Empty,
            MonthlyRent = a.Unit?.MonthlyRent ?? 0,
            ApplicantFullName = a.ApplicantFullName,
            ApplicantEmail = a.ApplicantEmail,
            Status = a.Status,
            CreatedAt = a.CreatedAt,
            SubmittedAt = a.SubmittedAt,
            ClaimedByName = a.ClaimedByUser?.FullName,
            IsClaimedByCurrentUser = a.ClaimedByUserId == CurrentUserId,
            CanEdit = !IsPropertyManager && a.IsEditable,
            CanWithdraw = !IsPropertyManager && !a.Status.IsTerminal(),
            CanReview = IsPropertyManager && (a.Status == ApplicationStatus.Submitted || a.Status == ApplicationStatus.UnderReview)
        }).ToList();

        var result = new PagedGridResult<ApplicationListItemViewModel>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(result);
    }
}
