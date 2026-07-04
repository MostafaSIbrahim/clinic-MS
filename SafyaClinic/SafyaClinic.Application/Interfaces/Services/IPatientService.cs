using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Patient;

namespace SafyaClinic.Application.Interfaces.Services;

public interface IPatientService
{
    Task<ServiceResult<PatientDto>> CreatePatientAsync(CreatePatientRequest request, int createdByUserId);
    Task<ServiceResult<PatientDto>> GetPatientByIdAsync(int patientId);
    Task<ServiceResult<PagedResult<PatientSummaryDto>>> SearchPatientsAsync(PaginationRequest request);
    Task<ServiceResult> UpdateBasicInfoAsync(int patientId, UpdatePatientBasicRequest request);
    Task<ServiceResult> UpdateMedicalInfoAsync(int patientId, UpdatePatientMedicalRequest request);
    Task<ServiceResult> AddPhoneAsync(int patientId, CreatePatientPhoneRequest request);
    Task<ServiceResult> RemovePhoneAsync(int patientId, int phoneId);
    Task<ServiceResult> AddAddressAsync(int patientId, CreatePatientAddressRequest request);
    Task<ServiceResult> RemoveAddressAsync(int patientId, int addressId);
}