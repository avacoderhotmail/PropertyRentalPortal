using System.ComponentModel.DataAnnotations;

namespace PropertyRentalPortal.Web.Models;

public class ResidenceHistoryInputModel
{
    public int Id { get; set; }

    [Required]
    public int RentalApplicationId { get; set; }

    [Required(ErrorMessage = "Street address is required.")]
    [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters.")]
    [Display(Name = "Street Address")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "City is required.")]
    [StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "State is required.")]
    [StringLength(50)]
    public string State { get; set; } = string.Empty;

    [Required(ErrorMessage = "Zip Code is required.")]
    [StringLength(20)]
    [Display(Name = "Zip Code")]
    public string ZipCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Landlord name is required.")]
    [StringLength(100, ErrorMessage = "Landlord name cannot exceed 100 characters.")]
    [Display(Name = "Landlord Name")]
    public string LandlordName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Landlord phone is required.")]
    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [StringLength(30)]
    [Display(Name = "Landlord Phone")]
    public string LandlordPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Move-in date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Move-In Date")]
    public DateTime MoveInDate { get; set; } = DateTime.UtcNow.AddYears(-1);

    [DataType(DataType.Date)]
    [Display(Name = "Move-Out Date (leave blank if current)")]
    public DateTime? MoveOutDate { get; set; }
}

public class ResidenceItemViewModel
{
    public int Id { get; set; }
    public int RentalApplicationId { get; set; }
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string LandlordName { get; set; } = string.Empty;
    public string LandlordPhone { get; set; } = string.Empty;
    public DateTime MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }

    public string DateRangeDisplay => MoveOutDate.HasValue
        ? $"{MoveInDate:MMM yyyy} – {MoveOutDate.Value:MMM yyyy}"
        : $"{MoveInDate:MMM yyyy} – Present";
}
