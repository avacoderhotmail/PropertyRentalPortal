using Microsoft.AspNetCore.Mvc;
using PropertyRentalPortal.Web.Models;

namespace PropertyRentalPortal.Web.ViewComponents;

public class PropertyManagerNotesViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(int applicationId, IEnumerable<NoteItemViewModel> notes, bool isPropertyManager)
    {
        // Enforce: Never render notes if viewer is not a property manager!
        if (!isPropertyManager)
        {
            return Content(string.Empty);
        }

        var model = new PropertyManagerNotesViewModel
        {
            ApplicationId = applicationId,
            Notes = notes.OrderByDescending(n => n.CreatedAt).ToList(),
            NewNote = new PropertyManagerNoteInputModel { RentalApplicationId = applicationId }
        };

        return View(model);
    }
}

public class PropertyManagerNotesViewModel
{
    public int ApplicationId { get; set; }
    public List<NoteItemViewModel> Notes { get; set; } = new();
    public PropertyManagerNoteInputModel NewNote { get; set; } = new();
}
