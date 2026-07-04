using SafyaClinic.Application.DTOs.Analysis;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.MedicalRecord;

namespace SafyaClinic.Application.Interfaces.Services;

public interface IAnalysisService
{
    Task<ServiceResult<MedicalAnalysisDto>> RequestAnalysisAsync(RequestAnalysisRequest request, int requestedBy);

    // Registers several analysis types at once for the same patient/visit
    // (e.g. CBC + Liver Function + Kidney Function requested together),
    // returning every created analysis so they can be printed on one slip.
    Task<ServiceResult<IEnumerable<MedicalAnalysisDto>>> RequestAnalysesAsync(RequestAnalysisBatchRequest request, int requestedBy);

    Task<ServiceResult<MedicalAnalysisDto>> GetAnalysisByIdAsync(int analysisId);
    Task<ServiceResult<IEnumerable<MedicalAnalysisDto>>> GetPatientAnalysesAsync(int patientId);

    // Analyses tied to a specific medical record (so the record page can show
    // which analyses were ordered from it, and the analysis page can link back).
    Task<ServiceResult<IEnumerable<MedicalAnalysisDto>>> GetAnalysesByRecordAsync(int recordId);

    // Global, paginated, searchable list of analyses across all patients —
    // backs the "/Analysis" landing page.
    Task<ServiceResult<PagedResult<MedicalAnalysisDto>>> SearchAnalysesAsync(PaginationRequest request, string? status = null);

    Task<ServiceResult> UpdateStatusAsync(int analysisId, UpdateAnalysisStatusRequest request);
    Task<ServiceResult> AddAttachmentAsync(int analysisId, string filePath, string fileName, string contentType, long fileSize, int uploadedBy);
    Task<ServiceResult> DeleteAttachmentAsync(int attachmentId);  // Admin only

    // Needed so the controller can stream/download an attachment's file bytes.
    Task<ServiceResult<AttachmentDto>> GetAttachmentAsync(int attachmentId);

    Task<ServiceResult<IEnumerable<AnalysisTypeDto>>> GetAnalysisTypesAsync();
}