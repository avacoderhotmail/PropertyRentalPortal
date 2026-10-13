using System.ComponentModel.DataAnnotations;
using PropertyRentalPortal.Web.Data.Enums;

namespace PropertyRentalPortal.Web.Models;

public class ApplicantInfoInputModel
{
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(120, ErrorMessage = "Full Name cannot exceed 120 characters.")]
    [Display(Name = "Full Legal Name")]
    public string ApplicantFullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email Address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(150)]
    [Display(Name = "Email Address")]
    public string ApplicantEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone Number is required.")]
    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [StringLength(30)]
    [Display(Name = "Phone Number")]
    public string ApplicantPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Current street address is required.")]
    [StringLength(200)]
    [Display(Name = "Current Street Address")]
    public string CurrentAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "City is required.")]
    [StringLength(100)]
    [Display(Name = "City")]
    public string CurrentCity { get; set; } = string.Empty;

    [Required(ErrorMessage = "State is required.")]
    [StringLength(50)]
    [Display(Name = "State")]
    public string CurrentState { get; set; } = string.Empty;

    [Required(ErrorMessage = "Zip Code is required.")]
    [StringLength(20)]
    [Display(Name = "Zip Code")]
    public string CurrentZipCode { get; set; } = string.Empty;
}

public class ApplicationWizardViewModel
{
    public int ApplicationId { get; set; }
    public int UnitId { get; set; }
    public string UnitNumber { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
    public string PropertyAddress { get; set; } = string.Empty;
    public decimal MonthlyRent { get; set; }
    public int Bedrooms { get; set; }
    public decimal Bathrooms { get; set; }
    public string UnitTypeName { get; set; } = string.Empty;

    public ApplicationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }

    /// <summary>
    /// Current section active on the single page:
    /// 1 = Applicant Information
    /// 2 = Residence History
    /// 3 = Summary
    /// </summary>
    public int CurrentSection { get; set; } = 1;

    /// <summary>
    /// Server-side decision: Editable only if Draft or Returned AND current user is the Applicant.
    /// In all other statuses or if viewer is not applicant, every section is read-only.
    /// </summary>
    public bool IsReadOnly { get; set; }

    public bool IsPropertyManager { get; set; }
    public bool CanWithdraw { get; set; }

    public bool ApplicantInfoSaved { get; set; }
    public bool ResidenceHistorySaved { get; set; }

    public bool CanSubmit => ApplicantInfoSaved && ResidenceHistorySaved && (Status == ApplicationStatus.Draft || Status == ApplicationStatus.Returned);

    /// <summary>
    /// Captures which button was clicked on post: "Continue", "Back", or "Submit"
    /// </summary>
    public string ActionClicked { get; set; } = string.Empty;

    // Section 1 fields
    public ApplicantInfoInputModel ApplicantInfo { get; set; } = new();

    // Section 2 fields
    public List<ResidenceItemViewModel> Residences { get; set; } = new();

    // Concurrency RowVersion
    public string? RowVersionString { get; set; }

    // History and Review Info (for summary and PM review view)
    public List<ApplicationStatusHistoryItemViewModel> StatusHistory { get; set; } = new();
    public List<NoteItemViewModel> PropertyManagerNotes { get; set; } = new();

    // Review Outcome (if submitted/under review)
    public ReviewApplicationViewModel? ReviewModel { get; set; }

    // Review queue status
    public string? ClaimedByName { get; set; }
    public bool IsClaimedByCurrentUser { get; set; }
}

public class ApplicationStatusHistoryItemViewModel
{
    public int Id { get; set; }
    public ApplicationStatus? OldStatus { get; set; }
    public ApplicationStatus NewStatus { get; set; }
    public string ChangedByName { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string? Comment { get; set; }
}
