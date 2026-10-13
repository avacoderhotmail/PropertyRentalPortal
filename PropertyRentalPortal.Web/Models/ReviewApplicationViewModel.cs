using System.ComponentModel.DataAnnotations;
using PropertyRentalPortal.Web.Data.Enums;

namespace PropertyRentalPortal.Web.Models;

public class ReviewApplicationViewModel : IValidatableObject
{
    [Required]
    public int ApplicationId { get; set; }

    public string ApplicantName { get; set; } = string.Empty;
    public string UnitDisplay { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a review outcome.")]
    [Display(Name = "Review Outcome")]
    public ReviewOutcome Outcome { get; set; } = ReviewOutcome.Approve;

    [Display(Name = "Review Comment")]
    [StringLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters.")]
    public string? Comment { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Requirement: "outcome of Approve, Return, or Deny and a comment (required for Return and Deny)."
        if ((Outcome == ReviewOutcome.Return || Outcome == ReviewOutcome.Deny) && string.IsNullOrWhiteSpace(Comment))
        {
            yield return new ValidationResult(
                $"A comment is required when {(Outcome == ReviewOutcome.Return ? "returning" : "denying")} an application.",
                new[] { nameof(Comment) });
        }
    }
}
