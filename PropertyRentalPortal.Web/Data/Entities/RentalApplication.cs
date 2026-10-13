using System.ComponentModel.DataAnnotations;
using PropertyRentalPortal.Web.Data.Enums;

namespace PropertyRentalPortal.Web.Data.Entities;

public class RentalApplication
{
    public int Id { get; set; }

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    [Required]
    public string ApplicantUserId { get; set; } = string.Empty;
    public ApplicationUser ApplicantUser { get; set; } = null!;

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;

    /// <summary>
    /// Current active step/section in the single-page wizard:
    /// 1 = Applicant Information, 2 = Residence History, 3 = Summary
    /// </summary>
    public int CurrentSection { get; set; } = 1;

    public bool ApplicantInfoSaved { get; set; } = false;
    public bool ResidenceHistorySaved { get; set; } = false;

    // --- Section 1: Applicant Information ---
    [Required]
    [MaxLength(120)]
    public string ApplicantFullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string ApplicantEmail { get; set; } = string.Empty;

    [Required]
    [Phone]
    [MaxLength(30)]
    public string ApplicantPhone { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string CurrentAddress { get; set; } = string.Empty;

    [MaxLength(100)]
    public string CurrentCity { get; set; } = string.Empty;

    [MaxLength(50)]
    public string CurrentState { get; set; } = string.Empty;

    [MaxLength(20)]
    public string CurrentZipCode { get; set; } = string.Empty;

    // --- Section 2: Residence History ---
    public ICollection<ResidenceHistory> Residences { get; set; } = new List<ResidenceHistory>();

    // --- Tracking and Review Queue ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Bonus Review Queue: Claiming
    public string? ClaimedByUserId { get; set; }
    public ApplicationUser? ClaimedByUser { get; set; }
    public DateTime? ClaimedAt { get; set; }

    public ICollection<ApplicationStatusHistory> StatusHistories { get; set; } = new List<ApplicationStatusHistory>();
    public ICollection<PropertyManagerNote> Notes { get; set; } = new List<PropertyManagerNote>();

    public Lease? Lease { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public bool IsEditable => Status.IsEditable();
    public bool CanSubmit => ApplicantInfoSaved && ResidenceHistorySaved && (Status == ApplicationStatus.Draft || Status == ApplicationStatus.Returned);
}
