using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Nutrition;

namespace SafyaClinic.Application.Interfaces.Services;

public interface INutritionService
{
    // Packages
    Task<ServiceResult<NutritionPackageDto>> CreatePackageAsync(CreateNutritionPackageDto request, int createdBy);
    Task<ServiceResult<NutritionPackageDto>> GetPackageByIdAsync(int packageId);
    Task<ServiceResult<IEnumerable<NutritionPackageDto>>> GetActivePackagesAsync();
    Task<ServiceResult> DeactivatePackageAsync(int packageId);

    // Enrollments
    Task<ServiceResult<PatientEnrollmentDto>> EnrollPatientAsync(CreateEnrollmentDto request, int enrolledBy);
    Task<ServiceResult<PatientEnrollmentDto>> GetEnrollmentByIdAsync(int enrollmentId);
    Task<ServiceResult<IEnumerable<PatientEnrollmentDto>>> GetPatientEnrollmentsAsync(int patientId);
    Task<ServiceResult> UpdateEnrollmentStatusAsync(int enrollmentId, string status);

    // Weekly follow-ups
    Task<ServiceResult<WeeklyFollowUpDto>> RecordFollowUpAsync(int enrollmentId, RecordFollowUpDto request, int recordedBy);
    Task<ServiceResult<WeeklyFollowUpDto>> GetFollowUpByIdAsync(int followUpId);
    Task<ServiceResult<IEnumerable<WeeklyFollowUpDto>>> GetEnrollmentFollowUpsAsync(int enrollmentId);
    Task<ServiceResult> CompleteFollowUpAsync(int followUpId);
    Task<ServiceResult<WeeklyFollowUpDto>> UpdateFollowUpAsync(int followUpId, RecordFollowUpDto request, int updatedBy);
    Task<ServiceResult> DeleteFollowUpAsync(int followUpId);

    // ── Injection Type catalog (feature 5) ────────────────────
    Task<ServiceResult<IEnumerable<InjectionTypeDto>>> GetInjectionTypesAsync(bool includeInactive = false);
    Task<ServiceResult<InjectionTypeDto>> GetInjectionTypeByIdAsync(int id);
    Task<ServiceResult<InjectionTypeDto>> CreateInjectionTypeAsync(CreateInjectionTypeDto request);
    Task<ServiceResult<InjectionTypeDto>> UpdateInjectionTypeAsync(int id, UpdateInjectionTypeDto request);
    Task<ServiceResult> DeleteInjectionTypeAsync(int id);

    // ── Vitamin Type catalog (feature 6) ───────────────────────
    Task<ServiceResult<IEnumerable<VitaminTypeDto>>> GetVitaminTypesAsync(bool includeInactive = false);
    Task<ServiceResult<VitaminTypeDto>> GetVitaminTypeByIdAsync(int id);
    Task<ServiceResult<VitaminTypeDto>> CreateVitaminTypeAsync(CreateVitaminTypeDto request);
    Task<ServiceResult<VitaminTypeDto>> UpdateVitaminTypeAsync(int id, UpdateVitaminTypeDto request);
    Task<ServiceResult> DeleteVitaminTypeAsync(int id);
}