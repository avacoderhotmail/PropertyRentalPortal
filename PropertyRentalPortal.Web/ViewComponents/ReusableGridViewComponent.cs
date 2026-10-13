using Microsoft.AspNetCore.Mvc;
using PropertyRentalPortal.Web.Models;

namespace PropertyRentalPortal.Web.ViewComponents;

public class ReusableGridViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(ApplicationListViewModel model)
    {
        return View(model);
    }
}
