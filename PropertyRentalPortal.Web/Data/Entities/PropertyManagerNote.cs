using System.ComponentModel.DataAnnotations;

namespace PropertyRentalPortal.Web.Data.Entities;

public class PropertyManagerNote
{
    public int Id { get; set; }

    public int RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;

    [Required]
    public string AuthorUserId { get; set; } = string.Empty;
    public ApplicationUser AuthorUser { get; set; } = null!;

    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
