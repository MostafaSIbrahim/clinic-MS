using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Settings;

namespace SafyaClinic.Application.Interfaces.Services;

public interface IClinicService
{
    Task<ServiceResult<IEnumerable<ClinicDto>>> GetAllAsync(bool includeInactive = true);
    Task<ServiceResult<ClinicDto>> GetByIdAsync(int id);
    Task<ServiceResult<ClinicDto>> CreateAsync(CreateClinicRequest request);
    Task<ServiceResult> UpdateAsync(int id, UpdateClinicRequest request);
    Task<ServiceResult> DeleteAsync(int id);

    // ── Clinic ⇄ Source agreements ───────────────────────────────
    Task<ServiceResult<ClinicSourceAgreementDto>> UpsertAgreementAsync(UpsertClinicSourceAgreementRequest request);
    Task<ServiceResult> RemoveAgreementAsync(int agreementId);
}
