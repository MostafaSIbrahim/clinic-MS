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
    CreatePatientRecordRequest request,
    int createdBy,
    bool isAdmin)
    {
        if (!request.ReservationId.HasValue)
        {
            return await CreateRecordCoreAsync(
                request,
                createdBy,
                isAdmin);
        }

        if (request.ReservationId.Value <= 0)
        {
            return ServiceResult<PatientRecordDto>.Failure(
                "Invalid reservation.");
        }

        try
        {
            return await _uow.ExecuteReservationRecordAsync(
                request.ReservationId.Value,
                () => CreateRecordCoreAsync(request, createdBy, isAdmin),
                result => result.IsSuccess);
        }
        catch (TimeoutException)
        {
            return ServiceResult<PatientRecordDto>.Failure(
                "Another request is creating this reservation's medical record. " +
                "Your submission was not saved. Return to the queue and " +
                "select Open Consultation before trying again.");
        }
    }
    private async Task<ServiceResult<PatientRecordDto>> CreateRecordCoreAsync(
        CreatePatientRecordRequest request,
        int createdBy,
        bool isAdmin)
    {
        if (createdBy <= 0)
            return ServiceResult<PatientRecordDto>.Failure("Invalid user.");

        if (!isAdmin && request.DoctorId != createdBy)
        {
            return ServiceResult<PatientRecordDto>.Failure(
                "You can only create records under your own doctor account.");
        }

        if (request.ReservationId.HasValue)
        {
            var reservation = await _uow.Reservations
                .Query()
                .Where(r => r.Id == request.ReservationId.Value)
                .Select(r => new
                {
                    r.PatientId,
                    r.DoctorId,
                    r.QueueStatus,
                    StatusName = r.Status.StatusName,
                    HasRecord = r.PatientRecord != null
                })
                .FirstOrDefaultAsync();

            if (reservation is null)
                return ServiceResult<PatientRecordDto>.Failure(
                    "Reservation not found.");

            if (reservation.PatientId != request.PatientId ||
                reservation.DoctorId != request.DoctorId)
            {
                return ServiceResult<PatientRecordDto>.Failure(
                    "The patient and doctor must match the reservation.");
            }

            if (!isAdmin && reservation.DoctorId != createdBy)
            {
                return ServiceResult<PatientRecordDto>.Failure(
                    "You are not the assigned doctor.");
            }

            if (reservation.StatusName != "Confirmed" ||
                reservation.QueueStatus != PatientQueueStatus.InConsultation)
            {
                return ServiceResult<PatientRecordDto>.Failure(
                    "The reservation must be in consultation before creating its record.");
            }

            if (reservation.HasRecord)
            {
                return ServiceResult<PatientRecordDto>.Failure(
                    "This reservation already has a medical record. " +
                    "Return to the queue and select Open Consultation.");
            }
        }
        var users = _uow.Users.Query();

        var validation = await _uow.Patients
            .Query()
            .Where(p => p.Id == request.PatientId)
            .Select(_ => new
            {
                DoctorExists = users.Any(u => u.Id == request.DoctorId)
            })
            .FirstOrDefaultAsync();

        if (validation is null)
            return ServiceResult<PatientRecordDto>.Failure("Patient not found.");

        if (!validation.DoctorExists)
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
        var recordState = await _uow.PatientRecords
            .Query()
            .Where(r => r.Id == recordId)
            .Select(r => new
            {
                r.IsLocked
            })
            .FirstOrDefaultAsync();
        if (recordState is null) 
            return ServiceResult.Failure("Record not found.");
        if (recordState.IsLocked) 
            return ServiceResult.Failure("Record is locked and cannot be edited.");

        var affectedRecord = await _uow.PatientRecords
            .Query()
            .Where(r => r.Id == recordId && !r.IsLocked)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.ChiefComplaint, request.ChiefComplaint == null
                                    ? null
                                    : request.ChiefComplaint.Trim())
                .SetProperty(r => r.PresentIllnessHistory, request.PresentIllnessHistory == null
                                    ? null
                                    : request.PresentIllnessHistory.Trim())
                .SetProperty(r => r.Diagnosis, request.Diagnosis == null
                                    ? null
                                    : request.Diagnosis.Trim())
                .SetProperty(r => r.DifferentialDiagnosis, request.DifferentialDiagnosis == null
                                    ? null
                                    : request.DifferentialDiagnosis.Trim())
                .SetProperty(r => r.TreatmentPlan, request.TreatmentPlan == null
                                    ? null
                                    : request.TreatmentPlan.Trim())
                .SetProperty(r => r.Notes, request.Notes == null
                                    ? null
                                    : request.Notes.Trim())
                .SetProperty(r => r.FollowUpDate, request.FollowUpDate)
                .SetProperty(r => r.UpdatedAt, DateTime.UtcNow));

        if (affectedRecord == 0)
            return ServiceResult.Failure("Record not found or locked.");

        return ServiceResult.Success("Record updated.");
    }

    public async Task<ServiceResult> LockRecordAsync(int recordId)
    {
        var updatedAt = DateTime.UtcNow;

        var affectedRows = await _uow.PatientRecords.Query()
            .Where(r => r.Id == recordId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.IsLocked, true)
                .SetProperty(r => r.UpdatedAt, updatedAt));

        return affectedRows == 0
            ? ServiceResult.Failure("Record not found.")
            : ServiceResult.Success("Record locked.");
    }
    public async Task<ServiceResult<FollowUpBookingContextDto>>
    GetFollowUpBookingContextAsync(
        int recordId,
        int currentUserId,
        bool canManageAll)
    {
        if (recordId <= 0 || currentUserId <= 0)
            return ServiceResult<FollowUpBookingContextDto>.Failure(
                "Invalid record or user.");

        var context = await _uow.PatientRecords
            .Query()
            .Where(r =>
                r.Id == recordId &&
                (canManageAll || r.DoctorId == currentUserId) &&
                r.FollowUpDate.HasValue &&
                r.FollowUpStatus == PatientFollowUpStatus.Pending &&
                r.FollowUpReservationId == null)
            .Select(r => new FollowUpBookingContextDto
            {
                RecordId = r.Id,
                PatientId = r.PatientId,
                PatientName = r.Patient.FirstName + " " + r.Patient.LastName,
                DoctorId = r.DoctorId,
                DoctorName = r.Doctor.FullName,
                Category = r.Category.ToString(),
                FollowUpDate = r.FollowUpDate.Value
            })
            .FirstOrDefaultAsync();

        return context is null
            ? ServiceResult<FollowUpBookingContextDto>.Failure(
                "This follow-up is unavailable, already handled, or belongs to another doctor.")
            : ServiceResult<FollowUpBookingContextDto>.Success(context);
    }
    public async Task<ServiceResult> DismissFollowUpAsync(
    int recordId,
    string? reason,
    int currentUserId,
    bool canManageAll)
    {
        if (recordId <= 0 || currentUserId <= 0)
            return ServiceResult.Failure("Invalid record or user.");

        var dismissalReason = reason?.Trim();

        if (string.IsNullOrWhiteSpace(dismissalReason))
            return ServiceResult.Failure("Enter a dismissal reason.");

        if (dismissalReason.Length > 500)
            return ServiceResult.Failure(
                "The dismissal reason must not exceed 500 characters.");

        try
        {
            // Use the same lock as booking this follow-up.
            return await _uow.ExecuteFollowUpBookingAsync(
                recordId,
                async () =>
                {
                    var updatedAtUtc = DateTime.UtcNow;

                    var affectedRows = await _uow.PatientRecords
                        .Query()
                        .Where(r =>
                            r.Id == recordId &&
                            (canManageAll || r.DoctorId == currentUserId) &&
                            r.FollowUpDate.HasValue &&
                            r.FollowUpStatus == PatientFollowUpStatus.Pending &&
                            r.FollowUpReservationId == null)
                        .ExecuteUpdateAsync(setters => setters
                            .SetProperty(
                                r => r.FollowUpStatus,
                                PatientFollowUpStatus.Dismissed)
                            .SetProperty(
                                r => r.FollowUpDismissalReason,
                                dismissalReason)
                            .SetProperty(
                                r => r.FollowUpUpdatedAtUtc,
                                (DateTime?)updatedAtUtc)
                            .SetProperty(
                                r => r.FollowUpUpdatedBy,
                                (int?)currentUserId)
                            .SetProperty(
                                r => r.UpdatedAt,
                                (DateTime?)updatedAtUtc));

                    return affectedRows == 0
                        ? ServiceResult.Failure(
                            "Dismissal was not applied. The follow-up may already " +
                            "be booked or dismissed, or you do not have access.")
                        : ServiceResult.Success("Follow-up dismissed.");
                },
                result => result.IsSuccess);
        }
        catch (TimeoutException)
        {
            return ServiceResult.Failure(
                "Another request is updating this follow-up. " +
                "Refresh the list before trying again.");
        }
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
        var treatmentState = await _uow.Treatments.Query()
                .Where(t => t.Id == treatmentId)
                .Select(t => new
                {
                    IsRecordLocked = t.Record.IsLocked
                })
                .FirstOrDefaultAsync();

        if (treatmentState is null)
            return ServiceResult.Failure("Treatment not found.");

        if (treatmentState.IsRecordLocked)
            return ServiceResult.Failure("Record is locked.");

        var affectedRows = await _uow.Treatments.Query()
            .Where(t => t.Id == treatmentId && !t.Record.IsLocked)
            .ExecuteDeleteAsync();

        return affectedRows == 0
            ? ServiceResult.Failure("Treatment not found or record is locked.")
            : ServiceResult.Success("Treatment removed.");
    }

    // ── Prescriptions ─────────────────────────────────────────

    public async Task<ServiceResult<PrescriptionDetailDto>> CreatePrescriptionAsync(
        CreatePrescriptionRequest request, int createdBy)
    {
        var record = await _uow.PatientRecords
            .Query()
            .Where(r => r.Id == request.RecordId)
            .Select(r => new
            {
                r.IsLocked
            })
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
        var itemState = await _uow.PrescriptionItems.Query()
                .Where(i => i.Id == itemId)
                .Select(i => new
                {
                    IsRecordLocked = i.Prescription.Record.IsLocked
                })
                .FirstOrDefaultAsync();

        if (itemState is null)
            return ServiceResult.Failure("Item not found.");

        if (itemState.IsRecordLocked)
            return ServiceResult.Failure("Record is locked.");

        var affectedRows = await _uow.PrescriptionItems.Query()
            .Where(i => i.Id == itemId && !i.Prescription.Record.IsLocked)
            .ExecuteDeleteAsync();

        return affectedRows == 0
            ? ServiceResult.Failure("Item not found or record is locked.")
            : ServiceResult.Success("Item removed.");
    }

    public async Task<ServiceResult> MarkPrescriptionPrintedAsync(int prescriptionId)
    {
        var affectedRows = await _uow.Prescriptions.Query()
            .Where(p => p.Id == prescriptionId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.IsPrinted, true));

        return affectedRows == 0
            ? ServiceResult.Failure("Prescription not found.")
            : ServiceResult.Success();
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
        var affectedRows = await _uow.PrescriptionAttachments.Query()
            .Where(a => a.Id == attachmentId)
            .ExecuteDeleteAsync();

        return affectedRows == 0
            ? ServiceResult.Failure("Attachment not found.")
            : ServiceResult.Success("Attachment deleted.");
    }

    public async Task<ServiceResult<AttachmentDto>> GetAttachmentAsync(int attachmentId)
    {
        var users = _uow.Users.Query();
        var a = await _uow.PrescriptionAttachments
            .Query()
            .Where(attachment => attachment.Id == attachmentId)
            .Select(attachment => new AttachmentDto
            {
                Id = attachment.Id,
                FileName = attachment.FileName,
                FilePath = attachment.FilePath,
                ContentType = attachment.ContentType ?? string.Empty,
                FileSizeBytes = attachment.FileSizeBytes ?? 0,
                UploadedAt = attachment.UploadedAt,
                UploadedBy = users
                    .Where(u => u.Id == attachment.UploadedBy)
                    .Select(u => u.FullName)
                    .FirstOrDefault() ?? string.Empty
            })
            .FirstOrDefaultAsync();
        if (a is null) return ServiceResult<AttachmentDto>.Failure("Attachment not found.");

        return ServiceResult<AttachmentDto>.Success(a);
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
    // ── Follow-ups ─────────────────────────────────────────────────
    public async Task<ServiceResult<PagedResult<PatientFollowUpDto>>>
    GetPendingFollowUpsAsync(
        PatientFollowUpFilter filter,
        PaginationRequest pagination,
        int currentUserId,
        bool canViewAll)
    {
        if (currentUserId <= 0)
        {
            return ServiceResult<PagedResult<PatientFollowUpDto>>.Failure(
                "Invalid user.");
        }

        if (filter.DoctorId is <= 0)
        {
            return ServiceResult<PagedResult<PatientFollowUpDto>>.Failure(
                "Invalid doctor.");
        }

        var due = (filter.Due ?? "All").Trim().ToLowerInvariant();

        if (due != "all" &&
            due != "overdue" &&
            due != "today" &&
            due != "upcoming")
        {
            return ServiceResult<PagedResult<PatientFollowUpDto>>.Failure(
                "Invalid due-date filter.");
        }

        var from = filter.From?.Date;
        var to = filter.To?.Date;

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return ServiceResult<PagedResult<PatientFollowUpDto>>.Failure(
                "The start date must not be after the end date.");
        }

        if (to == DateTime.MaxValue.Date)
        {
            return ServiceResult<PagedResult<PatientFollowUpDto>>.Failure(
                "The end date is outside the supported range.");
        }

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var query = _uow.PatientRecords
            .Query()
            .Where(r =>
                r.FollowUpDate.HasValue &&
                r.FollowUpStatus == PatientFollowUpStatus.Pending &&
                r.FollowUpReservationId == null);

        // canViewAll must come from the authenticated user's roles.
        var doctorId = canViewAll
            ? filter.DoctorId
            : currentUserId;

        if (doctorId.HasValue)
            query = query.Where(r => r.DoctorId == doctorId.Value);

        if (due == "overdue")
        {
            query = query.Where(r => r.FollowUpDate < today);
        }
        else if (due == "today")
        {
            query = query.Where(r =>
                r.FollowUpDate >= today &&
                r.FollowUpDate < tomorrow);
        }
        else if (due == "upcoming")
        {
            query = query.Where(r => r.FollowUpDate >= tomorrow);
        }

        if (from.HasValue)
            query = query.Where(r => r.FollowUpDate >= from.Value);

        if (to.HasValue)
        {
            var endExclusive = to.Value.AddDays(1);
            query = query.Where(r => r.FollowUpDate < endExclusive);
        }

        var totalCount = await query.CountAsync();

        var totalPages = Math.Max(
            1,
            (int)Math.Ceiling((double)totalCount / pagination.PageSize));

        var page = Math.Min(pagination.Page, totalPages);

        var items = await query
            .OrderBy(r => r.FollowUpDate)
            .ThenBy(r => r.Id)
            .Skip((page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(r => new PatientFollowUpDto
            {
                RecordId = r.Id,
                PatientId = r.PatientId,
                PatientName = r.Patient.FirstName + " " + r.Patient.LastName,

                DoctorId = r.DoctorId,
                DoctorName = r.Doctor.FullName,

                FollowUpDate = r.FollowUpDate.Value,
                OriginalReservationId = r.ReservationId
            })
            .ToListAsync();

        return ServiceResult<PagedResult<PatientFollowUpDto>>.Success(
            new PagedResult<PatientFollowUpDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pagination.PageSize
            });
    }

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
