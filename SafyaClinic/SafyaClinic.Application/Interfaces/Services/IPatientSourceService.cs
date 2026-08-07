using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Settings;

namespace SafyaClinic.Application.Interfaces.Services;

public interface IPatientSourceService
{
    Task<ServiceResult<IEnumerable<PatientSourceDto>>> GetAllAsync(bool includeInactive = true);
    Task<ServiceResult<PatientSourceDto>> GetByIdAsync(int id);
    Task<ServiceResult<PatientSourceDto>> CreateAsync(CreatePatientSourceRequest request);
    Task<ServiceResult> UpdateAsync(int id, UpdatePatientSourceRequest request);
    Task<ServiceResult> DeleteAsync(int id);
}
