using Microsoft.AspNetCore.Mvc;
using PropertyRentalPortal.Web.Data.Enums;

namespace PropertyRentalPortal.Web.ViewComponents;

public class StatusBadgeViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(ApplicationStatus status, string? size = null)
    {
        var model = new StatusBadgeViewModel
        {
            Status = status,
            Size = size ?? "normal"
        };
        return View(model);
    }
}

public class StatusBadgeViewModel
{
    public ApplicationStatus Status { get; set; }
    public string Size { get; set; } = "normal";

    public string BadgeClass => Status switch
    {
        ApplicationStatus.Draft => "bg-secondary text-white",
        ApplicationStatus.Submitted => "bg-primary text-white",
        ApplicationStatus.UnderReview => "bg-warning text-dark",
        ApplicationStatus.Returned => "bg-info text-dark",
        ApplicationStatus.Approved => "bg-success text-white",
        ApplicationStatus.Denied => "bg-danger text-white",
        ApplicationStatus.Withdrawn => "bg-dark text-white",
        _ => "bg-light text-dark"
    };

    public string IconClass => Status switch
    {
        ApplicationStatus.Draft => "bi-pencil-square",
        ApplicationStatus.Submitted => "bi-send",
        ApplicationStatus.UnderReview => "bi-hourglass-split",
        ApplicationStatus.Returned => "bi-arrow-counterclockwise",
        ApplicationStatus.Approved => "bi-check-circle-fill",
        ApplicationStatus.Denied => "bi-x-circle-fill",
        ApplicationStatus.Withdrawn => "bi-slash-circle",
        _ => "bi-info-circle"
    };

    public string DisplayText => Status switch
    {
        ApplicationStatus.Draft => "Draft",
        ApplicationStatus.Submitted => "Submitted",
        ApplicationStatus.UnderReview => "Under Review",
        ApplicationStatus.Returned => "Returned",
        ApplicationStatus.Approved => "Approved",
        ApplicationStatus.Denied => "Denied",
        ApplicationStatus.Withdrawn => "Withdrawn",
        _ => Status.ToString()
    };
}
