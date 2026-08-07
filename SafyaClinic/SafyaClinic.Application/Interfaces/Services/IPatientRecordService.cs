using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.MedicalRecord;

namespace SafyaClinic.Application.Interfaces.Services;

public interface IPatientRecordService
{
    Task<ServiceResult<PatientRecordDto>> CreateRecordAsync(CreatePatientRecordRequest request, int createdBy);
    Task<ServiceResult<PatientRecordDto>> GetRecordByIdAsync(int recordId);
    Task<ServiceResult<IEnumerable<PatientRecordDto>>> GetPatientRecordsAsync(int patientId);
    Task<ServiceResult> UpdateRecordAsync(int recordId, UpdatePatientRecordRequest request);
    Task<ServiceResult> LockRecordAsync(int recordId);

    // Treatments
    Task<ServiceResult<TreatmentDto>> AddTreatmentAsync(int recordId, AddTreatmentRequest request, int createdBy);
    Task<ServiceResult> RemoveTreatmentAsync(int treatmentId);
    Task<ServiceResult<IEnumerable<TreatmentTypeDto>>> GetTreatmentTypesAsync(string? category = null);

    // Prescriptions
    Task<ServiceResult<PrescriptionDto>> AddPrescriptionAsync(int recordId, AddPrescriptionRequest request, int createdBy);
    Task<ServiceResult> MarkPrescriptionPrintedAsync(int prescriptionId);
    Task<ServiceResult> AddPrescriptionAttachmentAsync(int prescriptionId, string filePath, string fileName, string contentType, long fileSize, int uploadedBy);
    Task<ServiceResult> DeleteAttachmentAsync(int attachmentId);  // Admin only
    Task<ServiceResult<AttachmentDto>> GetAttachmentAsync(int attachmentId);
    Task<ServiceResult<PrescriptionPrintDto>> GetPrescriptionForPrintAsync(int prescriptionId);
}