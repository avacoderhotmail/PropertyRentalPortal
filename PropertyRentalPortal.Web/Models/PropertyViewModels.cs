using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PropertyRentalPortal.Web.Models;

public class PropertyFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Property Name is required.")]
    [StringLength(120, ErrorMessage = "Property Name cannot exceed 120 characters.")]
    [Display(Name = "Property Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Street Address is required.")]
    [StringLength(200, ErrorMessage = "Street Address cannot exceed 200 characters.")]
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

    [StringLength(500)]
    public string? Description { get; set; }
}

public class UnitFormViewModel
{
    public int Id { get; set; }

    [Required]
    public int PropertyId { get; set; }

    public string PropertyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Unit number is required.")]
    [StringLength(30, ErrorMessage = "Unit number cannot exceed 30 characters.")]
    [Display(Name = "Unit Number")]
    public string UnitNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Number of bedrooms is required.")]
    [Range(0, 10, ErrorMessage = "Bedrooms must be between 0 and 10.")]
    public int Bedrooms { get; set; } = 1;

    [Required(ErrorMessage = "Number of bathrooms is required.")]
    [Range(0.5, 10.0, ErrorMessage = "Bathrooms must be between 0.5 and 10.0.")]
    public decimal Bathrooms { get; set; } = 1.0m;

    [Required(ErrorMessage = "Monthly rent is required.")]
    [Range(100.0, 50000.0, ErrorMessage = "Monthly rent must be between $100 and $50,000.")]
    [Display(Name = "Monthly Rent ($)")]
    public decimal MonthlyRent { get; set; }

    [Required(ErrorMessage = "Please select a unit type.")]
    [Display(Name = "Unit Type")]
    public int UnitTypeId { get; set; }

    public List<SelectListItem> AvailableUnitTypes { get; set; } = new();
}

public class PropertyListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullAddress { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int TotalUnits { get; set; }
    public int AvailableUnits { get; set; }
}

public class UnitListItemViewModel
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public int Bedrooms { get; set; }
    public decimal Bathrooms { get; set; }
    public decimal MonthlyRent { get; set; }
    public string UnitTypeName { get; set; } = string.Empty;
    public bool IsUnitTypeActive { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime? ActiveLeaseEndDate { get; set; }
}
