
namespace SafyaClinic.Application.Services;

using global::SafyaClinic.Application.DTOs.Common;
using global::SafyaClinic.Application.DTOs.MedicalRecord;
using global::SafyaClinic.Application.Interfaces.Services;
using global::SafyaClinic.Domain.Entities.MedicalRecord;
using global::SafyaClinic.Domain.Entities.Prescription;
using global::SafyaClinic.Domain.Enums;
using global::SafyaClinic.Domain.Interfaces.Repositories;

public class PatientRecordService : IPatientRecordService
{
    private readonly IUnitOfWork _uow;

    public PatientRecordService(IUnitOfWork uow) => _uow = uow;

    public async Task<ServiceResult<PatientRecordDto>> CreateRecordAsync(
        CreatePatientRecordRequest request, int createdBy)
    {
        if (!await _uow.Patients.ExistsAsync(request.PatientId))
            return ServiceResult<PatientRecordDto>.Failure("Patient not found.");
        if (!await _uow.Users.ExistsAsync(request.DoctorId))
            return ServiceResult<PatientRecordDto>.Failure("Doctor not found.");
        if (!Enum.TryParse<TreatmentCategory>(request.Category, out var category))
            return ServiceResult<PatientRecordDto>.Failure("Invalid category.");

        var record = new PatientRecord
        {
            PatientId = request.PatientId,
            DoctorId = request.DoctorId,
            ReservationId = request.ReservationId,
            Category = category,
            ChiefComplaint = request.ChiefComplaint?.Trim(),
            PresentIllnessHistory = request.PresentIllnessHistory?.Trim(),
            Diagnosis = request.Diagnosis?.Trim(),
            DifferentialDiagnosis = request.DifferentialDiagnosis?.Trim(),
            TreatmentPlan = request.TreatmentPlan?.Trim(),
            Notes = request.Notes?.Trim(),
            FollowUpDate = request.FollowUpDate,
            IsLocked = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        await _uow.PatientRecords.AddAsync(record);
        await _uow.SaveChangesAsync();
        return ServiceResult<PatientRecordDto>.Success(await BuildRecordDtoAsync(record));
    }

    public async Task<ServiceResult<PatientRecordDto>> GetRecordByIdAsync(int recordId)
    {
        var record = await _uow.PatientRecords.GetByIdAsync(recordId);
        if (record is null) return ServiceResult<PatientRecordDto>.Failure("Record not found.");
        return ServiceResult<PatientRecordDto>.Success(await BuildRecordDtoAsync(record));
    }

    public async Task<ServiceResult<IEnumerable<PatientRecordDto>>> GetPatientRecordsAsync(int patientId)
    {
        var records = await _uow.PatientRecords.FindAsync(r => r.PatientId == patientId);
        var dtos = new List<PatientRecordDto>();
        foreach (var r in records.OrderByDescending(r => r.CreatedAt))
            dtos.Add(await BuildRecordDtoAsync(r));
        return ServiceResult<IEnumerable<PatientRecordDto>>.Success(dtos);
    }

    public async Task<ServiceResult> UpdateRecordAsync(int recordId, UpdatePatientRecordRequest request)
    {
        var record = await _uow.PatientRecords.GetByIdAsync(recordId);
        if (record is null) return ServiceResult.Failure("Record not found.");
        if (record.IsLocked) return ServiceResult.Failure("Record is locked and cannot be edited.");

        record.ChiefComplaint = request.ChiefComplaint?.Trim();
        record.PresentIllnessHistory = request.PresentIllnessHistory?.Trim();
        record.Diagnosis = request.Diagnosis?.Trim();
        record.DifferentialDiagnosis = request.DifferentialDiagnosis?.Trim();
        record.TreatmentPlan = request.TreatmentPlan?.Trim();
        record.Notes = request.Notes?.Trim();
        record.FollowUpDate = request.FollowUpDate;
        record.UpdatedAt = DateTime.UtcNow;

        _uow.PatientRecords.Update(record);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Record updated.");
    }

    public async Task<ServiceResult> LockRecordAsync(int recordId)
    {
        var record = await _uow.PatientRecords.GetByIdAsync(recordId);
        if (record is null) return ServiceResult.Failure("Record not found.");

        record.IsLocked = true;
        record.UpdatedAt = DateTime.UtcNow;
        _uow.PatientRecords.Update(record);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Record locked.");
    }

    // ── Treatments ────────────────────────────────────────────

    public async Task<ServiceResult<TreatmentDto>> AddTreatmentAsync(
        int recordId, AddTreatmentRequest request, int createdBy)
    {
        var record = await _uow.PatientRecords.GetByIdAsync(recordId);
        if (record is null) return ServiceResult<TreatmentDto>.Failure("Record not found.");
        if (record.IsLocked) return ServiceResult<TreatmentDto>.Failure("Record is locked.");

        var treatment = new Treatment
        {
            RecordId = recordId,
            TreatmentTypeId = request.TreatmentTypeId,
            Description = request.Description.Trim(),
            Cost = request.Cost,
            PerformedDate = request.PerformedDate,
            Notes = request.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        await _uow.Treatments.AddAsync(treatment);
        await _uow.SaveChangesAsync();

        TreatmentType? tt = treatment.TreatmentTypeId.HasValue
            ? await _uow.TreatmentTypes.GetByIdAsync(treatment.TreatmentTypeId.Value)
            : null;

        return ServiceResult<TreatmentDto>.Success(new TreatmentDto
        {
            Id = treatment.Id,
            TreatmentTypeId = treatment.TreatmentTypeId,
            TypeName = tt?.TypeName,
            Description = treatment.Description,
            Cost = treatment.Cost,
            PerformedDate = treatment.PerformedDate,
            Notes = treatment.Notes
        });
    }

    public async Task<ServiceResult> RemoveTreatmentAsync(int treatmentId)
    {
        var t = await _uow.Treatments.GetByIdAsync(treatmentId);
        if (t is null) return ServiceResult.Failure("Treatment not found.");

        var record = await _uow.PatientRecords.GetByIdAsync(t.RecordId);
        if (record?.IsLocked == true) return ServiceResult.Failure("Record is locked.");

        _uow.Treatments.Delete(t);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Treatment removed.");
    }

    public async Task<ServiceResult<IEnumerable<TreatmentTypeDto>>> GetTreatmentTypesAsync(
        string? category = null)
    {
        var types = await _uow.TreatmentTypes.FindAsync(t => t.IsActive);

        if (!string.IsNullOrWhiteSpace(category) &&
            Enum.TryParse<TreatmentCategory>(category, out var cat))
            types = types.Where(t => t.Category == cat);

        return ServiceResult<IEnumerable<TreatmentTypeDto>>.Success(
            types.Select(t => new TreatmentTypeDto
            {
                Id = t.Id,
                Category = t.Category.ToString(),
                TypeName = t.TypeName,
                Description = t.Description,
                DefaultCost = t.DefaultCost,
                DurationMinutes = t.DurationMinutes,
                IsActive = t.IsActive
            }));
    }

    // ── Prescriptions ─────────────────────────────────────────

    public async Task<ServiceResult<PrescriptionDto>> AddPrescriptionAsync(
        int recordId, AddPrescriptionRequest request, int createdBy)
    {
        var record = await _uow.PatientRecords.GetByIdAsync(recordId);
        if (record is null) return ServiceResult<PrescriptionDto>.Failure("Record not found.");
        if (record.IsLocked) return ServiceResult<PrescriptionDto>.Failure("Record is locked.");

        var prescription = new Prescription
        {
            RecordId = recordId,
            MedicationName = request.MedicationName.Trim(),
            Dosage = request.Dosage?.Trim(),
            Frequency = request.Frequency?.Trim(),
            Duration = request.Duration?.Trim(),
            RouteOfAdministration = request.RouteOfAdministration?.Trim(),
            Instructions = request.Instructions?.Trim(),
            IsPrinted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        await _uow.Prescriptions.AddAsync(prescription);
        await _uow.SaveChangesAsync();

        return ServiceResult<PrescriptionDto>.Success(new PrescriptionDto
        {
            Id = prescription.Id,
            MedicationName = prescription.MedicationName,
            Dosage = prescription.Dosage,
            Frequency = prescription.Frequency,
            Duration = prescription.Duration,
            RouteOfAdministration = prescription.RouteOfAdministration,
            Instructions = prescription.Instructions,
            IsPrinted = prescription.IsPrinted,
            CreatedAt = prescription.CreatedAt
        });
    }

    public async Task<ServiceResult> MarkPrescriptionPrintedAsync(int prescriptionId)
    {
        var p = await _uow.Prescriptions.GetByIdAsync(prescriptionId);
        if (p is null) return ServiceResult.Failure("Prescription not found.");

        p.IsPrinted = true;
        _uow.Prescriptions.Update(p);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> AddPrescriptionAttachmentAsync(
        int prescriptionId, string filePath, string fileName,
        string contentType, long fileSize, int uploadedBy)
    {
        if (!await _uow.Prescriptions.ExistsAsync(prescriptionId))
            return ServiceResult.Failure("Prescription not found.");

        await _uow.PrescriptionAttachments.AddAsync(new PrescriptionAttachment
        {
            PrescriptionId = prescriptionId,
            FileName = fileName,
            FilePath = filePath,
            ContentType = contentType,
            FileSizeBytes = fileSize,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = uploadedBy
        });
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Attachment added.");
    }

    public async Task<ServiceResult> DeleteAttachmentAsync(int attachmentId)
    {
        var a = await _uow.PrescriptionAttachments.GetByIdAsync(attachmentId);
        if (a is null) return ServiceResult.Failure("Attachment not found.");

        _uow.PrescriptionAttachments.Delete(a);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Attachment deleted.");
    }

    public async Task<ServiceResult<AttachmentDto>> GetAttachmentAsync(int attachmentId)
    {
        var a = await _uow.PrescriptionAttachments.GetByIdAsync(attachmentId);
        if (a is null) return ServiceResult<AttachmentDto>.Failure("Attachment not found.");

        return ServiceResult<AttachmentDto>.Success(new AttachmentDto
        {
            Id = a.Id,
            FileName = a.FileName,
            FilePath = a.FilePath,
            ContentType = a.ContentType,
            FileSizeBytes = (long)a.FileSizeBytes,
            UploadedAt = a.UploadedAt
        });
    }

    public async Task<ServiceResult<PrescriptionPrintDto>> GetPrescriptionForPrintAsync(int prescriptionId)
    {
        var prescription = await _uow.Prescriptions.GetByIdAsync(prescriptionId);
        if (prescription is null)
            return ServiceResult<PrescriptionPrintDto>.Failure("Prescription not found.");

        var record = await _uow.PatientRecords.GetByIdAsync(prescription.RecordId);
        if (record is null)
            return ServiceResult<PrescriptionPrintDto>.Failure("Medical record not found.");

        var patient = await _uow.Patients.GetByIdAsync(record.PatientId);
        var doctor = await _uow.Users.GetByIdAsync(record.DoctorId);

        return ServiceResult<PrescriptionPrintDto>.Success(new PrescriptionPrintDto
        {
            Id = prescription.Id,
            MedicationName = prescription.MedicationName,
            Dosage = prescription.Dosage,
            Frequency = prescription.Frequency,
            Duration = prescription.Duration,
            RouteOfAdministration = prescription.RouteOfAdministration,
            Instructions = prescription.Instructions,
            CreatedAt = prescription.CreatedAt,
            RecordId = record.Id,
            Diagnosis = record.Diagnosis,
            PatientId = record.PatientId,
            PatientName = patient is null ? "" : $"{patient.FirstName} {patient.LastName}",
            PatientAge = patient?.DateOfBirth.HasValue == true
                ? (int)((DateTime.Today - patient.DateOfBirth!.Value).TotalDays / 365.25)
                : null,
            PatientGender = patient?.Gender?.ToString(),
            DoctorName = doctor?.FullName ?? "",
            DoctorSpecialization = doctor?.Specialization,
            DoctorLicenseNumber = doctor?.LicenseNumber
        });
    }

    // ── Mapper ────────────────────────────────────────────────

    private async Task<PatientRecordDto> BuildRecordDtoAsync(PatientRecord record)
    {
        var patient = await _uow.Patients.GetByIdAsync(record.PatientId);
        var doctor = await _uow.Users.GetByIdAsync(record.DoctorId);
        var treatments = await _uow.Treatments.FindAsync(t => t.RecordId == record.Id);
        var prescriptions = await _uow.Prescriptions.FindAsync(p => p.RecordId == record.Id);

        var treatmentDtos = new List<TreatmentDto>();
        foreach (var t in treatments)
        {
            TreatmentType? tt = t.TreatmentTypeId.HasValue
                ? await _uow.TreatmentTypes.GetByIdAsync(t.TreatmentTypeId.Value) : null;
            treatmentDtos.Add(new TreatmentDto
            {
                Id = t.Id,
                TreatmentTypeId = t.TreatmentTypeId,
                TypeName = tt?.TypeName,
                Description = t.Description,
                Cost = t.Cost,
                PerformedDate = t.PerformedDate,
                Notes = t.Notes
            });
        }

        var prescriptionDtos = new List<PrescriptionDto>();
        foreach (var p in prescriptions)
        {
            var attachments = await _uow.PrescriptionAttachments.FindAsync(a => a.PrescriptionId == p.Id);
            var uploader = p.CreatedBy > 0 ? await _uow.Users.GetByIdAsync(p.CreatedBy) : null;
            prescriptionDtos.Add(new PrescriptionDto
            {
                Id = p.Id,
                MedicationName = p.MedicationName,
                Dosage = p.Dosage,
                Frequency = p.Frequency,
                Duration = p.Duration,
                RouteOfAdministration = p.RouteOfAdministration,
                Instructions = p.Instructions,
                IsPrinted = p.IsPrinted,
                CreatedAt = p.CreatedAt,
                Attachments = attachments.Select(a => new AttachmentDto
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    FilePath = a.FilePath,
                    ContentType = a.ContentType,
                    FileSizeBytes = (long)a.FileSizeBytes,
                    UploadedAt = a.UploadedAt,
                    UploadedBy = uploader?.FullName ?? ""
                })
            });
        }

        return new PatientRecordDto
        {
            Id = record.Id,
            PatientId = record.PatientId,
            PatientName = patient is null ? "" : $"{patient.FirstName} {patient.LastName}",
            DoctorId = record.DoctorId,
            DoctorName = doctor?.FullName ?? "",
            ReservationId = record.ReservationId,
            Category = record.Category.ToString(),
            ChiefComplaint = record.ChiefComplaint,
            PresentIllnessHistory = record.PresentIllnessHistory,
            Diagnosis = record.Diagnosis,
            DifferentialDiagnosis = record.DifferentialDiagnosis,
            TreatmentPlan = record.TreatmentPlan,
            Notes = record.Notes,
            FollowUpDate = record.FollowUpDate,
            IsLocked = record.IsLocked,
            CreatedAt = record.CreatedAt,
            Treatments = treatmentDtos,
            Prescriptions = prescriptionDtos
        };
    }
}