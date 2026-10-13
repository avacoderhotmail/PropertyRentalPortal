using Microsoft.AspNetCore.Mvc;
using PropertyRentalPortal.Web.Models;

namespace PropertyRentalPortal.Web.ViewComponents;

public class ApplicationTimelineViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(IEnumerable<ApplicationStatusHistoryItemViewModel> history)
    {
        var items = history.OrderByDescending(h => h.ChangedAt).ToList();
        return View(items);
    }
}
