using PropertyRentalPortal.Web.Data.Entities;
using PropertyRentalPortal.Web.Data.Enums;
using PropertyRentalPortal.Web.Models;
using PropertyRentalPortal.Web.Services.Common;

namespace PropertyRentalPortal.Web.Services;

public interface IApplicationService
{
    // Queries
    Task<RentalApplication?> GetApplicationByIdAsync(int id, string? requestingUserId = null, bool isManager = false);
    Task<List<RentalApplication>> GetApplicationsListAsync(
        string? userId,
        bool isManager,
        ApplicationStatus? status = null,
        int? propertyId = null,
        string? sortBy = null,
        bool sortDescending = false,
        int page = 1,
        int pageSize = 10);
    Task<int> GetApplicationsCountAsync(
        string? userId,
        bool isManager,
        ApplicationStatus? status = null,
        int? propertyId = null);

    // Lifecycle & Wizard Actions
    Task<ServiceResult<RentalApplication>> StartApplicationAsync(int unitId, string applicantUserId, string applicantName, string applicantEmail);
    Task<ServiceResult<RentalApplication>> SaveApplicantInfoAsync(int applicationId, string applicantUserId, ApplicantInfoInputModel model);
    Task<ServiceResult<RentalApplication>> ValidateResidenceHistorySectionAsync(int applicationId, string applicantUserId);
    Task<ServiceResult<RentalApplication>> SubmitApplicationAsync(int applicationId, string applicantUserId);
    Task<ServiceResult> WithdrawApplicationAsync(int applicationId, string applicantUserId, string? reason);

    // Residence History Modal CRUD
    Task<ResidenceHistory?> GetResidenceByIdAsync(int residenceId);
    Task<ServiceResult<ResidenceHistory>> AddResidenceAsync(int applicationId, string applicantUserId, ResidenceHistoryInputModel model);
    Task<ServiceResult<ResidenceHistory>> UpdateResidenceAsync(int residenceId, string applicantUserId, ResidenceHistoryInputModel model);
    Task<ServiceResult> DeleteResidenceAsync(int residenceId, string applicantUserId);

    // Property Manager Review
    Task<ServiceResult<RentalApplication>> ReviewApplicationAsync(
        int applicationId,
        string managerUserId,
        ReviewOutcome outcome,
        string? comment);

    // Bonus: Review Queue
    Task<ServiceResult> ClaimApplicationAsync(int applicationId, string managerUserId);
    Task<ServiceResult> ReleaseApplicationAsync(int applicationId, string managerUserId);

    // Bonus: Property Manager Notes
    Task<List<PropertyManagerNote>> GetNotesAsync(int applicationId);
    Task<ServiceResult<PropertyManagerNote>> AddNoteAsync(int applicationId, string managerUserId, string content);
    Task<ServiceResult> DeleteNoteAsync(int noteId, string managerUserId);
}
