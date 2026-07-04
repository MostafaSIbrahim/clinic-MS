using MedicalRecordDtos = SafyaClinic.Application.DTOs.MedicalRecord;
namespace SafyaClinic.Application.DTOs.Analysis;

public class MedicalAnalysisDto
{
    public int Id { get; init; }
    public int PatientId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public int DoctorId { get; init; }
    public string DoctorName { get; init; } = string.Empty;
    public int? RecordId { get; init; }
    public int AnalysisTypeId { get; init; }
    public string AnalysisTypeName { get; init; } = string.Empty;
    public string? PreparationInstructions { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsUrgent { get; init; }
    public DateTime RequestDate { get; init; }
    public DateTime? ResultDate { get; init; }
    public string? ResultNotes { get; init; }
    public IEnumerable<MedicalRecordDtos.AttachmentDto> Attachments { get; init; }
        = Enumerable.Empty<MedicalRecordDtos.AttachmentDto>();
}
 
// Bring AttachmentDto into scope here

 
public class RequestAnalysisRequest
{
    public int PatientId { get; init; }
    public int DoctorId { get; init; }
    public int? RecordId { get; init; }
    public int AnalysisTypeId { get; init; }
    public bool IsUrgent { get; init; }
}

// Allows registering several analyses for the same patient/visit in one go,
// so they can all be printed on a single request slip for the patient/lab.
public class RequestAnalysisBatchRequest
{
    public int PatientId { get; init; }
    public int DoctorId { get; init; }
    public int? RecordId { get; init; }
    public List<int> AnalysisTypeIds { get; init; } = new();
    public bool IsUrgent { get; init; }
}

public class UpdateAnalysisStatusRequest
{
    public string Status { get; init; } = string.Empty;
    public DateTime? ResultDate { get; init; }
    public string? ResultNotes { get; init; }
}

public class AnalysisTypeDto
{
    public int Id { get; init; }
    public string TypeName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal? DefaultCost { get; init; }
    public string? PreparationInstructions { get; init; }
}
