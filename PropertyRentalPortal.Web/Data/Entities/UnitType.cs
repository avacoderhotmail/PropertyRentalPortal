using System.ComponentModel.DataAnnotations;

namespace PropertyRentalPortal.Web.Data.Entities;

public class UnitType
{
    public int Id { get; set; }

    [Required]
    [MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<Unit> Units { get; set; } = new List<Unit>();
}
