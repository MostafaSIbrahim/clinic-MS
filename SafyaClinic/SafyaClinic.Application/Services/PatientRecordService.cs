
namespace SafyaClinic.Application.Services;

using global::SafyaClinic.Application.DTOs.Common;
using global::SafyaClinic.Application.DTOs.MedicalRecord;
using global::SafyaClinic.Application.Interfaces.Services;
using global::SafyaClinic.Domain.Entities.MedicalRecord;
using global::SafyaClinic.Domain.Entities.Prescription;
using global::SafyaClinic.Domain.Enums;
using global::SafyaClinic.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

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
        var dto = await PatientRecordDtoQuery()
     .FirstOrDefaultAsync(r => r.Id == record.Id);

        if (dto is null)
            return ServiceResult<PatientRecordDto>.Failure(
                "Medical record could not be loaded after creation.");

        return ServiceResult<PatientRecordDto>.Success(dto);
    }

    public async Task<ServiceResult<PatientRecordDto>> GetRecordByIdAsync(int recordId)
    {
        var record = await PatientRecordDetailDtoQuery()
            .Where(r => r.Id == recordId)
            .FirstOrDefaultAsync();

        if (record is null)
            return ServiceResult<PatientRecordDto>.Failure("Record not found.");

        return ServiceResult<PatientRecordDto>.Success(record);
    }

    public async Task<ServiceResult<IEnumerable<PatientRecordDto>>> GetPatientRecordsAsync(int patientId)
    {
        var dtos = await PatientRecordDtoQuery()
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

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
        var recordState = await _uow.PatientRecords.Query()
                .Where(r => r.Id == recordId)
                .Select(r => new
                {
                    r.IsLocked
                })
                .FirstOrDefaultAsync();

        if (recordState is null)
            return ServiceResult<TreatmentDto>.Failure("Record not found.");

        if (recordState.IsLocked)
            return ServiceResult<TreatmentDto>.Failure("Record is locked.");
        var treatment = new Treatment
        {
            RecordId = recordId,
            Description = request.Description.Trim(),
            Cost = request.Cost,
            PerformedDate = request.PerformedDate,
            Notes = request.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        await _uow.Treatments.AddAsync(treatment);
        await _uow.SaveChangesAsync();

        return ServiceResult<TreatmentDto>.Success(new TreatmentDto
        {
            Id = treatment.Id,
            Description = treatment.Description,
            Cost = treatment.Cost,
            PerformedDate = treatment.PerformedDate,
            Notes = treatment.Notes
        });
    }

    public async Task<ServiceResult> RemoveTreatmentAsync(int treatmentId)
    {
        var treatmentState = await _uow.Treatments.Query(asNoTracking: false)
                .Where(t => t.Id == treatmentId)
                .Select(t => new
                {
                    Treatment = t,
                    IsRecordLocked = t.Record.IsLocked
                })
                .FirstOrDefaultAsync();

        if (treatmentState is null)
            return ServiceResult.Failure("Treatment not found.");

        if (treatmentState.IsRecordLocked)
            return ServiceResult.Failure("Record is locked.");

        _uow.Treatments.Delete(treatmentState.Treatment);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Treatment removed.");
    }

    // ── Prescriptions ─────────────────────────────────────────

    public async Task<ServiceResult<PrescriptionDetailDto>> CreatePrescriptionAsync(
        CreatePrescriptionRequest request, int createdBy)
    {
        var record = await _uow.PatientRecords
            .Query()
            .Where(r => r.Id == request.RecordId)
            .FirstOrDefaultAsync();
        if (record is null) 
            return ServiceResult<PrescriptionDetailDto>.Failure("Record not found.");
        if (record.IsLocked) 
            return ServiceResult<PrescriptionDetailDto>.Failure("Record is locked.");

        var prescription = new Prescription
        {
            RecordId = request.RecordId,
            PrescriptionDate = DateTime.UtcNow,
            Notes = request.Notes?.Trim(),
            IsPrinted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        foreach (var itemReq in request.Items)
        {
            prescription.Items.Add(new PrescriptionItem
            {
                MedicationName = itemReq.MedicationName.Trim(),
                Dosage = itemReq.Dosage?.Trim(),
                Frequency = itemReq.Frequency?.Trim(),
                Duration = itemReq.Duration?.Trim(),
                RouteOfAdministration = itemReq.RouteOfAdministration?.Trim(),
                Instructions = itemReq.Instructions?.Trim()
            });
        }

        await _uow.Prescriptions.AddAsync(prescription);
        await _uow.SaveChangesAsync();

        var dto = await BuildPrescriptionDetailDtoAsync(prescription.Id);
        if (dto is null)
            return ServiceResult<PrescriptionDetailDto>.Failure(
                "Prescription could not be loaded after creation.");
        return ServiceResult<PrescriptionDetailDto>.Success(dto!);
    }

    public async Task<ServiceResult<PrescriptionDetailDto>> GetPrescriptionByIdAsync(int prescriptionId)
    {
        var prescription = await PrescriptionDetailDtoQuery()
            .FirstOrDefaultAsync(p => p.Id == prescriptionId);
        if (prescription is null)
            return ServiceResult<PrescriptionDetailDto>.Failure("Prescription not found.");

        return ServiceResult<PrescriptionDetailDto>.Success(prescription);
    }

    public async Task<ServiceResult<IEnumerable<PrescriptionListDto>>> GetPrescriptionsByRecordAsync(
      int recordId)
    {
        var users = _uow.Users.Query();
        var dtos = await _uow.Prescriptions.Query()
            .Where(p => p.RecordId == recordId)
            .OrderByDescending(p => p.PrescriptionDate)
            .Select(p => new PrescriptionListDto
            {
                Id = p.Id,
                PrescriptionDate = p.PrescriptionDate,
                Notes = p.Notes,
                IsPrinted = p.IsPrinted,
                DrugCount = p.Items.Count(),
                CreatedAt = p.CreatedAt,
                CreatedByName = p.CreatedBy > 0
                    ? (users
                        .Where(u => u.Id == p.CreatedBy)
                        .Select(u => u.FullName)
                        .FirstOrDefault() ?? string.Empty)
                    : string.Empty
                            })
            .ToListAsync();

        return ServiceResult<IEnumerable<PrescriptionListDto>>.Success(dtos);
    }

  

    public async Task<ServiceResult<PrescriptionItemDto>> AddPrescriptionItemAsync(
        int prescriptionId, AddPrescriptionItemRequest request)
    {
        var prescriptionState = await _uow.Prescriptions.Query()
                .Where(p => p.Id == prescriptionId)
                .Select(p => new
                {
                    IsRecordLocked = p.Record.IsLocked
                })
                .FirstOrDefaultAsync();

        if (prescriptionState is null)
            return ServiceResult<PrescriptionItemDto>.Failure("Prescription not found.");

        if (prescriptionState.IsRecordLocked)
            return ServiceResult<PrescriptionItemDto>.Failure("Record is locked.");
        var item = new PrescriptionItem
        {
            PrescriptionId = prescriptionId,
            MedicationName = request.MedicationName.Trim(),
            Dosage = request.Dosage?.Trim(),
            Frequency = request.Frequency?.Trim(),
            Duration = request.Duration?.Trim(),
            RouteOfAdministration = request.RouteOfAdministration?.Trim(),
            Instructions = request.Instructions?.Trim()
        };

        await _uow.PrescriptionItems.AddAsync(item);
        await _uow.SaveChangesAsync();

        return ServiceResult<PrescriptionItemDto>.Success(new PrescriptionItemDto
        {
            Id = item.Id,
            MedicationName = item.MedicationName,
            Dosage = item.Dosage,
            Frequency = item.Frequency,
            Duration = item.Duration,
            RouteOfAdministration = item.RouteOfAdministration,
            Instructions = item.Instructions
        });
    }

    public async Task<ServiceResult> RemovePrescriptionItemAsync(int itemId)
    {
        var itemState = await _uow.PrescriptionItems.Query(asNoTracking: false)
                .Where(i => i.Id == itemId)
                .Select(i => new
                {
                    Item = i,
                    IsRecordLocked = i.Prescription.Record.IsLocked
                })
                .FirstOrDefaultAsync();

        if (itemState is null)
            return ServiceResult.Failure("Item not found.");

        if (itemState.IsRecordLocked)
            return ServiceResult.Failure("Record is locked.");

        _uow.PrescriptionItems.Delete(itemState.Item);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Item removed.");
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
 
    // Attachments now link to Prescription (document)
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
            FileSizeBytes = (long)a.FileSizeBytes!,
            UploadedAt = a.UploadedAt
        });
    }

    public async Task<ServiceResult<PrescriptionPrintDto>> GetPrescriptionForPrintAsync(
    int prescriptionId)
    {
        var prescription = await _uow.Prescriptions.Query()
            .Where(p => p.Id == prescriptionId)
            .Select(p => new PrescriptionPrintDto
            {
                Id = p.Id,
                PrescriptionDate = p.PrescriptionDate,
                Notes = p.Notes,

                PatientName = $"{p.Record.Patient.FirstName} {p.Record.Patient.LastName}",
                PatientAge = p.Record.Patient.DateOfBirth.HasValue
                    ? (int)((DateTime.Today - p.Record.Patient.DateOfBirth.Value)
                        .TotalDays / 365.25)
                    : null,
                PatientGender = p.Record.Patient.Gender != null
                    ? p.Record.Patient.Gender.ToString()
                    : null,

                Diagnosis = p.Record.Diagnosis,
                DoctorName = p.Record.Doctor.FullName,
                DoctorSpecialization = p.Record.Doctor.Specialization,
                DoctorLicenseNumber = p.Record.Doctor.LicenseNumber,

                Items = p.Items.Select(i => new PrescriptionItemDto
                {
                    Id = i.Id,
                    MedicationName = i.MedicationName,
                    Dosage = i.Dosage,
                    Frequency = i.Frequency,
                    Duration = i.Duration,
                    RouteOfAdministration = i.RouteOfAdministration,
                    Instructions = i.Instructions
                })
            })
            .FirstOrDefaultAsync();

        return prescription is null
            ? ServiceResult<PrescriptionPrintDto>.Failure("Prescription not found.")
            : ServiceResult<PrescriptionPrintDto>.Success(prescription);
    }
    // ── Mapper ────────────────────────────────────────────────

    
    // ── Private mapper ────────────────────────────────────────
    private IQueryable<PatientRecordDto> PatientRecordDtoQuery()
    {
        return _uow.PatientRecords.Query()
            .Select(r => new PatientRecordDto
            {
                Id = r.Id,
                PatientId = r.PatientId,
                PatientName = $"{r.Patient.FirstName} {r.Patient.LastName}",
                DoctorId = r.DoctorId,
                DoctorName = r.Doctor.FullName,
                ReservationId = r.ReservationId,
                Category = r.Category.ToString(),
                ChiefComplaint = r.ChiefComplaint,
                PresentIllnessHistory = r.PresentIllnessHistory,
                Diagnosis = r.Diagnosis,
                DifferentialDiagnosis = r.DifferentialDiagnosis,
                TreatmentPlan = r.TreatmentPlan,
                Notes = r.Notes,
                FollowUpDate = r.FollowUpDate,
                IsLocked = r.IsLocked,
                CreatedAt = r.CreatedAt
            });
    }

    private IQueryable<PatientRecordDto> PatientRecordDetailDtoQuery()
    {
        return _uow.PatientRecords.Query()
            .Select(r => new PatientRecordDto
            {
                Id = r.Id,
                PatientId = r.PatientId,
                PatientName = $"{r.Patient.FirstName} {r.Patient.LastName}",
                DoctorId = r.DoctorId,
                DoctorName = r.Doctor.FullName,
                ReservationId = r.ReservationId,
                Category = r.Category.ToString(),
                ChiefComplaint = r.ChiefComplaint,
                PresentIllnessHistory = r.PresentIllnessHistory,
                Diagnosis = r.Diagnosis,
                DifferentialDiagnosis = r.DifferentialDiagnosis,
                TreatmentPlan = r.TreatmentPlan,
                Notes = r.Notes,
                FollowUpDate = r.FollowUpDate,
                IsLocked = r.IsLocked,
                CreatedAt = r.CreatedAt,

                Treatments = r.Treatments
                    .OrderByDescending(t => t.PerformedDate)
                    .Select(t => new TreatmentDto
                    {
                        Id = t.Id,
                        Description = t.Description,
                        Cost = t.Cost,
                        PerformedDate = t.PerformedDate,
                        Notes = t.Notes
                    })
            });
    }
    private Task<PrescriptionDetailDto?> BuildPrescriptionDetailDtoAsync(int prescriptionId)
    {
        return PrescriptionDetailDtoQuery()
            .FirstOrDefaultAsync(p => p.Id == prescriptionId);
    }
    private IQueryable<PrescriptionDetailDto> PrescriptionDetailDtoQuery()
    {
        var users = _uow.Users.Query();

        return _uow.Prescriptions.Query()
            .Select(p => new PrescriptionDetailDto
            {
                Id = p.Id,
                RecordId = p.RecordId,
                PrescriptionDate = p.PrescriptionDate,
                Notes = p.Notes,
                IsPrinted = p.IsPrinted,
                CreatedAt = p.CreatedAt,

                PatientName = $"{p.Record.Patient.FirstName} {p.Record.Patient.LastName}",
                PatientAge = p.Record.Patient.DateOfBirth.HasValue
                    ? (int)((DateTime.Today - p.Record.Patient.DateOfBirth.Value)
                        .TotalDays / 365.25)
                    : null,
                PatientGender = p.Record.Patient.Gender != null
                    ? p.Record.Patient.Gender.ToString()
                    : null,

                Diagnosis = p.Record.Diagnosis,
                DoctorName = p.Record.Doctor.FullName,
                DoctorSpecialization = p.Record.Doctor.Specialization,
                DoctorLicenseNumber = p.Record.Doctor.LicenseNumber,

                Items = p.Items.Select(i => new PrescriptionItemDto
                {
                    Id = i.Id,
                    MedicationName = i.MedicationName,
                    Dosage = i.Dosage,
                    Frequency = i.Frequency,
                    Duration = i.Duration,
                    RouteOfAdministration = i.RouteOfAdministration,
                    Instructions = i.Instructions
                }),

                Attachments = p.Attachments.Select(a => new AttachmentDto
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    FilePath = a.FilePath,
                    ContentType = a.ContentType ?? string.Empty,
                    FileSizeBytes = a.FileSizeBytes ?? 0,
                    UploadedAt = a.UploadedAt,
                    UploadedBy = users
                        .Where(u => u.Id == a.UploadedBy)
                        .Select(u => u.FullName)
                        .FirstOrDefault() ?? string.Empty
                })
            });
    }
}
