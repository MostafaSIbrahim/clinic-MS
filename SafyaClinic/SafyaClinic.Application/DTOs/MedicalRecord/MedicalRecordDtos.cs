namespace SafyaClinic.Application.DTOs.MedicalRecord;

// ── Patient Record ────────────────────────────────────────────

public class PatientRecordDto
{
    public int Id { get; init; }
    public int PatientId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public int DoctorId { get; init; }
    public string DoctorName { get; init; } = string.Empty;
    public int? ReservationId { get; init; }
    public string Category { get; init; } = string.Empty;
    public string? ChiefComplaint { get; init; }
    public string? PresentIllnessHistory { get; init; }
    public string? Diagnosis { get; init; }
    public string? DifferentialDiagnosis { get; init; }
    public string? TreatmentPlan { get; init; }
    public string? Notes { get; init; }
    public DateTime? FollowUpDate { get; init; }
    public bool IsLocked { get; init; }
    public DateTime CreatedAt { get; init; }

    public IEnumerable<TreatmentDto> Treatments { get; init; } = Enumerable.Empty<TreatmentDto>();
    public IEnumerable<PrescriptionDto> Prescriptions { get; init; } = Enumerable.Empty<PrescriptionDto>();
}

public class CreatePatientRecordRequest
{
    public int PatientId { get; init; }
    public int DoctorId { get; init; }
    public int? ReservationId { get; init; }
    public string Category { get; init; } = "InternalMedicine";
    public string? ChiefComplaint { get; init; }
    public string? PresentIllnessHistory { get; init; }
    public string? Diagnosis { get; init; }
    public string? DifferentialDiagnosis { get; init; }
    public string? TreatmentPlan { get; init; }
    public string? Notes { get; init; }
    public DateTime? FollowUpDate { get; init; }
}

public class UpdatePatientRecordRequest
{
    public string? ChiefComplaint { get; init; }
    public string? PresentIllnessHistory { get; init; }
    public string? Diagnosis { get; init; }
    public string? DifferentialDiagnosis { get; init; }
    public string? TreatmentPlan { get; init; }
    public string? Notes { get; init; }
    public DateTime? FollowUpDate { get; init; }
}

// ── Treatment ─────────────────────────────────────────────────

public class TreatmentDto
{
    public int Id { get; init; }
    public int? TreatmentTypeId { get; init; }
    public string? TypeName { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal? Cost { get; init; }
    public DateTime PerformedDate { get; init; }
    public string? Notes { get; init; }
}

public class AddTreatmentRequest
{
    public int? TreatmentTypeId { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal? Cost { get; init; }
    public DateTime PerformedDate { get; init; } = DateTime.Today;
    public string? Notes { get; init; }
}

public class TreatmentTypeDto
{
    public int Id { get; init; }
    public string Category { get; init; } = string.Empty;
    public string TypeName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal? DefaultCost { get; init; }
    public int DurationMinutes { get; init; }
    public bool IsActive { get; init; }
}

// ── Prescription ──────────────────────────────────────────────

public class PrescriptionDto
{
    public int Id { get; init; }
    public string MedicationName { get; init; } = string.Empty;
    public string? Dosage { get; init; }
    public string? Frequency { get; init; }
    public string? Duration { get; init; }
    public string? RouteOfAdministration { get; init; }
    public string? Instructions { get; init; }
    public bool IsPrinted { get; init; }
    public DateTime CreatedAt { get; init; }
    public IEnumerable<AttachmentDto> Attachments { get; init; } = Enumerable.Empty<AttachmentDto>();
}

public class AddPrescriptionRequest
{
    public string MedicationName { get; init; } = string.Empty;
    public string? Dosage { get; init; }
    public string? Frequency { get; init; }
    public string? Duration { get; init; }
    public string? RouteOfAdministration { get; init; }
    public string? Instructions { get; init; }
}

// ── Attachments ───────────────────────────────────────────────

public class AttachmentDto
{
    public int Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public DateTime UploadedAt { get; init; }
    public string UploadedBy { get; init; } = string.Empty;
}