using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyRentalPortal.Web.Data.Entities;

public class Unit
{
    public int Id { get; set; }

    public int PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    [Required]
    [MaxLength(30)]
    public string UnitNumber { get; set; } = string.Empty;

    public int Bedrooms { get; set; }

    [Column(TypeName = "decimal(4, 1)")]
    public decimal Bathrooms { get; set; } = 1.0m;

    [Column(TypeName = "decimal(18, 2)")]
    public decimal MonthlyRent { get; set; }

    public int UnitTypeId { get; set; }
    public UnitType UnitType { get; set; } = null!;

    public ICollection<Lease> Leases { get; set; } = new List<Lease>();
    public ICollection<RentalApplication> Applications { get; set; } = new List<RentalApplication>();

    /// <summary>
    /// Checks whether the unit currently has an active lease covering the specified date (defaults to today).
    /// Requirement: "A unit whose lease term covers today is not available."
    /// </summary>
    public bool HasActiveLease(DateTime? asOfDate = null)
    {
        var targetDate = (asOfDate ?? DateTime.UtcNow).Date;
        return Leases.Any(l => l.StartDate.Date <= targetDate && targetDate <= l.EndDate.Date);
    }
}
