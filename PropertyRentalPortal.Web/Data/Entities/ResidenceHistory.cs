using System.ComponentModel.DataAnnotations;

namespace PropertyRentalPortal.Web.Data.Entities;

public class ResidenceHistory
{
    public int Id { get; set; }

    public int RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string State { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string ZipCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LandlordName { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string LandlordPhone { get; set; } = string.Empty;

    public DateTime MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
