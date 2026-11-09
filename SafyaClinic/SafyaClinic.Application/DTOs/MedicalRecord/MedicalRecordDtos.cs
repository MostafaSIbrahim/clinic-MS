namespace SafyaClinic.Application.DTOs.MedicalRecord;

// ── Patient Record ────────────────────────────────────────────

/// <summary>P5c: one visit documented in a record.</summary>
public sealed record RecordVisitDto(int ReservationId, DateTime Date, TimeSpan Time, string DoctorName, string StatusName);

/// <summary>P5c: the patient's latest record of a kind, offered when a consultation starts.</summary>
public sealed record LatestRecordDto(int Id, bool IsLocked, DateTime CreatedAt, string DoctorName, string? Diagnosis);

public class PatientRecordDto
{
    /// <summary>P5c: the visits documented in this record, oldest first (filled when one record is loaded).</summary>
    public IReadOnlyList<RecordVisitDto> Visits { get; init; } = Array.Empty<RecordVisitDto>();
    public int Id { get; init; }
    public int PatientId { get; init; }
    /// <summary>P5a: filled when one record is loaded, for the allergy check when prescribing.</summary>
    public string? PatientAllergies { get; init; }
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
    public string Description { get; init; } = string.Empty;
    public decimal? Cost { get; init; }
    public DateTime PerformedDate { get; init; }
    public string? Notes { get; init; }
}

public class AddTreatmentRequest
{
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

// Everything the printable prescription form needs in one shape, so the
// print view doesn't have to reach into three different services.
public class PrescriptionPrintDto
{
    public int Id { get; init; }
    public DateTime PrescriptionDate { get; init; }
    public string? Notes { get; init; }

    public string PatientName { get; init; } = string.Empty;
    public int? PatientAge { get; init; }
    public string? PatientGender { get; init; }
    public string? Diagnosis { get; init; }

    public string DoctorName { get; init; } = string.Empty;
    public string? DoctorSpecialization { get; init; }
    public string? DoctorLicenseNumber { get; init; }

    public IEnumerable<PrescriptionItemDto> Items { get; init; } = Enumerable.Empty<PrescriptionItemDto>();
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
public class PrescriptionItemRequest
{
    public string MedicationName { get; init; } = string.Empty;
    public string? Dosage { get; init; }
    public string? Frequency { get; init; }
    public string? Duration { get; init; }
    public string? RouteOfAdministration { get; init; }
    public string? Instructions { get; init; }
}

public class AddPrescriptionItemRequest
{
    /// <summary>P5a: the doctor confirmed adding a medicine that matches a recorded allergy.</summary>
    public bool ConfirmAllergyOverride { get; init; }
    public int PrescriptionId { get; init; }
    public string MedicationName { get; init; } = string.Empty;
    public string? Dosage { get; init; }
    public string? Frequency { get; init; }
    public string? Duration { get; init; }
    public string? RouteOfAdministration { get; init; }
    public string? Instructions { get; init; }
}
// ── Prescription Document (list view) ─────────────────────────
public class PrescriptionListDto
{
    public int Id { get; init; }
    public DateTime PrescriptionDate { get; init; }
    public string? Notes { get; init; }
    public bool IsPrinted { get; init; }
    public int DrugCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public string CreatedByName { get; init; } = string.Empty;
}

// ── Prescription Detail (full view with all drugs) ────────────
public class PrescriptionDetailDto
{
    /// <summary>P5a: for the patient header and the allergy check.</summary>
    public int PatientId { get; init; }
    public string? PatientAllergies { get; init; }
    public int Id { get; init; }
    public int RecordId { get; init; }
    public DateTime PrescriptionDate { get; init; }
    public string? Notes { get; init; }
    public bool IsPrinted { get; init; }
    public DateTime CreatedAt { get; init; }

    public string PatientName { get; init; } = string.Empty;
    public int? PatientAge { get; init; }
    public string? PatientGender { get; init; }
    public string? Diagnosis { get; init; }

    public string DoctorName { get; init; } = string.Empty;
    public string? DoctorSpecialization { get; init; }
    public string? DoctorLicenseNumber { get; init; }

    public IEnumerable<PrescriptionItemDto> Items { get; init; } = Enumerable.Empty<PrescriptionItemDto>();
    public IEnumerable<AttachmentDto> Attachments { get; init; } = Enumerable.Empty<AttachmentDto>();
}

// ── Prescription Item (single drug line) ──────────────────────
public class PrescriptionItemDto
{
    public int Id { get; init; }
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
// ── Requests ──────────────────────────────────────────────────
public class CreatePrescriptionRequest
{
    /// <summary>P5a: the doctor confirmed adding medicines that match a recorded allergy.</summary>
    public bool ConfirmAllergyOverride { get; init; }
    public int RecordId { get; init; }
    public string? Notes { get; init; }
    public List<PrescriptionItemRequest> Items { get; init; } = new();
}


