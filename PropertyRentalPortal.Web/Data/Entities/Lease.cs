using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyRentalPortal.Web.Data.Entities;

public class Lease
{
    public int Id { get; set; }

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public int? ApplicationId { get; set; }
    public RentalApplication? RentalApplication { get; set; }

    public string TenantUserId { get; set; } = string.Empty;
    public ApplicationUser Tenant { get; set; } = null!;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal MonthlyRent { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
