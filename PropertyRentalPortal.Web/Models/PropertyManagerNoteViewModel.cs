using System.ComponentModel.DataAnnotations;

namespace PropertyRentalPortal.Web.Models;

public class PropertyManagerNoteInputModel
{
    [Required]
    public int RentalApplicationId { get; set; }

    [Required(ErrorMessage = "Note content cannot be empty.")]
    [StringLength(2000, ErrorMessage = "Note cannot exceed 2000 characters.")]
    [Display(Name = "Internal Management Note")]
    public string Content { get; set; } = string.Empty;
}

public class NoteItemViewModel
{
    public int Id { get; set; }
    public int RentalApplicationId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
