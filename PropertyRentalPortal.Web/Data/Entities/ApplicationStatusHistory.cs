using System.ComponentModel.DataAnnotations;
using PropertyRentalPortal.Web.Data.Enums;

namespace PropertyRentalPortal.Web.Data.Entities;

public class ApplicationStatusHistory
{
    public int Id { get; set; }

    public int RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;

    public ApplicationStatus? OldStatus { get; set; }
    public ApplicationStatus NewStatus { get; set; }

    [Required]
    public string ChangedByUserId { get; set; } = string.Empty;
    public ApplicationUser ChangedByUser { get; set; } = null!;

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    public string? Comment { get; set; }
}
