using Microsoft.AspNetCore.Mvc.Rendering;
using PropertyRentalPortal.Web.Data.Enums;

namespace PropertyRentalPortal.Web.Models;

public class ApplicationListItemViewModel
{
    public int Id { get; set; }
    public int UnitId { get; set; }
    public string UnitNumber { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
    public decimal MonthlyRent { get; set; }
    public string ApplicantFullName { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ClaimedByName { get; set; }
    public bool IsClaimedByCurrentUser { get; set; }
    public bool CanEdit { get; set; }
    public bool CanWithdraw { get; set; }
    public bool CanReview { get; set; }
}

public class ApplicationListFilterViewModel
{
    public ApplicationStatus? Status { get; set; }
    public int? PropertyId { get; set; }
    public string? SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class ApplicationListViewModel
{
    public ApplicationListFilterViewModel Filter { get; set; } = new();
    public List<ApplicationListItemViewModel> Applications { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / Filter.PageSize);
    public bool HasPreviousPage => Filter.Page > 1;
    public bool HasNextPage => Filter.Page < TotalPages;

    public bool IsPropertyManager { get; set; }
    public List<SelectListItem> Properties { get; set; } = new();
}

public class PagedGridResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
