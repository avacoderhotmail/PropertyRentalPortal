using Microsoft.AspNetCore.Identity;

namespace PropertyRentalPortal.Web.Data.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RentalApplication> Applications { get; set; } = new List<RentalApplication>();
    public ICollection<Lease> Leases { get; set; } = new List<Lease>();
    public ICollection<ApplicationStatusHistory> StatusChangesMade { get; set; } = new List<ApplicationStatusHistory>();
}
