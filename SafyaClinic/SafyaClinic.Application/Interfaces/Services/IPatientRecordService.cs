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

    // Prescriptions
    //Task<ServiceResult<PrescriptionDto>> AddPrescriptionAsync(int recordId, AddPrescriptionRequest request, int createdBy);
   // Task<ServiceResult> MarkPrescriptionPrintedAsync(int prescriptionId);
    //Task<ServiceResult> AddPrescriptionAttachmentAsync(int prescriptionId, string filePath, string fileName, string contentType, long fileSize, int uploadedBy);
    //Task<ServiceResult> DeleteAttachmentAsync(int attachmentId);  // Admin only
    //Task<ServiceResult<AttachmentDto>> GetAttachmentAsync(int attachmentId);
    //Task<ServiceResult<PrescriptionPrintDto>> GetPrescriptionForPrintAsync(int prescriptionId);
    // ── Prescriptions (Document-level) ─────────────────────────
    Task<ServiceResult<PrescriptionDetailDto>> CreatePrescriptionAsync(CreatePrescriptionRequest request, int createdBy);
    Task<ServiceResult<PrescriptionDetailDto>> GetPrescriptionByIdAsync(int prescriptionId);
    Task<ServiceResult<IEnumerable<PrescriptionListDto>>> GetPrescriptionsByRecordAsync(int recordId);
    Task<ServiceResult<PrescriptionItemDto>> AddPrescriptionItemAsync(int prescriptionId, AddPrescriptionItemRequest request);
    Task<ServiceResult> RemovePrescriptionItemAsync(int itemId);
    Task<ServiceResult> MarkPrescriptionPrintedAsync(int prescriptionId);

    // Attachments now belong to the Prescription document
    Task<ServiceResult> AddPrescriptionAttachmentAsync(int prescriptionId, string filePath, string fileName, string contentType, long fileSize, int uploadedBy);
    Task<ServiceResult> DeleteAttachmentAsync(int attachmentId);
    Task<ServiceResult<AttachmentDto>> GetAttachmentAsync(int attachmentId);
    Task<ServiceResult<PrescriptionPrintDto>> GetPrescriptionForPrintAsync(int prescriptionId);
}