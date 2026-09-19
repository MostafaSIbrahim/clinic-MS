using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.MedicalRecord;
using SafyaClinic.Application.DTOs.Reservation;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Domain.Entities.MedicalRecord;
using SafyaClinic.Domain.Entities.Payment;
using SafyaClinic.Domain.Entities.Reservation;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Interfaces.Repositories;
using System.Linq.Expressions;

namespace SafyaClinic.Application.Services;

public class ReservationService : IReservationService
{
    private readonly IUnitOfWork _uow;

    public ReservationService(IUnitOfWork uow) => _uow = uow;

    public async Task<ServiceResult<ReservationDto>> CreateReservationAsync(
        CreateReservationRequest request, int createdBy)
    {
        var users = _uow.Users.Query();
        var clinics = _uow.Clinics.Query();

        var validation = await _uow.Patients
            .Query()
            .Where(p => p.Id == request.PatientId)
            .Select(_ => new
            {
                DoctorExists = users.Any(u => u.Id == request.DoctorId),
                ClinicExists = clinics.Any(c => c.Id == request.ClinicId)
            })
            .FirstOrDefaultAsync();

        if (validation is null)
            return ServiceResult<ReservationDto>.Failure("Patient not found.");

        if (!validation.DoctorExists)
            return ServiceResult<ReservationDto>.Failure("Doctor not found.");

        if (!validation.ClinicExists)
            return ServiceResult<ReservationDto>.Failure("Clinic not found.");
        if (!Enum.TryParse<TreatmentCategory>(request.Category, out var category))
            return ServiceResult<ReservationDto>.Failure("Invalid category. Use 'InternalMedicine' or 'Nutritional'.");

        var treatmentType = await _uow.TreatmentTypes
                .Query()
                .Where(t => t.Id == request.TreatmentTypeId)
                .Select(t => new
                {
                    t.DefaultCost
                })
                .FirstOrDefaultAsync();

        if (treatmentType is null)
            return ServiceResult<ReservationDto>.Failure("Treatment type not found.");

        // The treatment type carries the price for this reservation; use the
        // caller-supplied amount only if they explicitly overrode it.
        var totalAmount = request.TotalAmount ?? treatmentType.DefaultCost;

        // Default status = Pending (ID 1)
        var reservation = new Reservation
        {
            PatientId = request.PatientId,
            DoctorId = request.DoctorId,
            ClinicId = request.ClinicId,
            TreatmentTypeId = request.TreatmentTypeId,
            StatusId = 1,
            Category = category,
            ReservationDate = request.ReservationDate,
            ReservationTime = request.ReservationTime,
            DurationMinutes = request.DurationMinutes,
            Reason = request.Reason?.Trim(),
            Notes = request.Notes?.Trim(),
            // A reservation whose treatment type genuinely costs nothing (e.g. a nutrition
            // "Follow-up" visit already covered by the enrollment package) has nothing left
            // to collect, so it's considered paid the moment it's created.
            IsPaid = totalAmount.HasValue && totalAmount.Value == 0m,
            TotalAmount = totalAmount,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        await _uow.Reservations.AddAsync(reservation);
        await _uow.SaveChangesAsync();

        // A reservation with no charge (e.g. a nutrition "Follow-up" visit) still gets a
        // real $0 Payment record, exactly like any other visit — just for a zero amount.
        // Without this, "free" visits would leave no trace in payment reports/dashboards
        // at all, since those are built from Payment rows, not reservations.
        if (reservation.IsPaid && totalAmount.HasValue && totalAmount.Value == 0m)
            await RecordZeroCostPaymentAsync(reservation, createdBy);

        var dto = await GetReservationDtoByIdAsync(reservation.Id);

        if (dto is null)
            return ServiceResult<ReservationDto>.Failure(
                "Reservation created but could not be loaded.");

        return ServiceResult<ReservationDto>.Success(dto);
    }

    public async Task<ServiceResult<ReservationDto>> GetReservationByIdAsync(int reservationId)
    {
        var reservation = await GetReservationDtoByIdAsync(reservationId);

        if (reservation is null)
            return ServiceResult<ReservationDto>.Failure("Reservation not found.");

        return ServiceResult<ReservationDto>.Success(reservation);
       
    }

    public async Task<ServiceResult<PagedResult<ReservationSummaryDto>>> GetReservationsAsync(
        ReservationFilterRequest filter, PaginationRequest pagination)
    {
        var query = _uow.Reservations.Query();

        if (filter.DoctorId.HasValue)
            query = query.Where(r => r.DoctorId == filter.DoctorId.Value);

        if (filter.PatientId.HasValue)
            query = query.Where(r => r.PatientId == filter.PatientId.Value);

        if (filter.ClinicId.HasValue)
            query = query.Where(r => r.ClinicId == filter.ClinicId.Value);

        if (filter.StatusId.HasValue)
            query = query.Where(r => r.StatusId == filter.StatusId.Value);

        if (!string.IsNullOrEmpty(filter.Category) &&
            Enum.TryParse<TreatmentCategory>(filter.Category, out var category))
        { 
            query = query.Where(r => r.Category == category); 
        }

        if (filter.DateFrom.HasValue)
            query = query.Where(r => r.ReservationDate >= filter.DateFrom.Value);

        if (filter.DateTo.HasValue)
            query = query.Where(r => r.ReservationDate <= filter.DateTo.Value);

        if (filter.IsPaid.HasValue)
            query = query.Where(r => r.IsPaid == filter.IsPaid.Value);

        var totalCount = await query.CountAsync();
        var summaries = await query
            .OrderByDescending(r=> r.ReservationDate)
            .ThenBy(r=> r.ReservationTime)
            .ThenBy(r => r.Id)
            .Skip((pagination.Page-1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(r => new ReservationSummaryDto
            {
                Id = r.Id,
                PatientId = r.PatientId,
                PatientName = r.Patient != null ? $"{r.Patient.FirstName} {r.Patient.LastName}" : "",
                DoctorName = r.Doctor != null ? r.Doctor.FullName : "",
                ClinicName = r.Clinic != null ? r.Clinic.Name : "",
                TreatmentTypeName = r.TreatmentType != null ? r.TreatmentType.TypeName : "",
                ReservationDate = r.ReservationDate,
                ReservationTime = r.ReservationTime,
                StatusName = r.Status != null ? r.Status.StatusName : "Unknown",
                StatusColor = r.Status != null ? r.Status.ColorCode : "#6c757d",
                Category = r.Category.ToString(),
                IsPaid = r.IsPaid
            }).ToListAsync();
        return ServiceResult<PagedResult<ReservationSummaryDto>>.Success(
            new PagedResult<ReservationSummaryDto>
            {
                Items = summaries,
                TotalCount = totalCount,
                Page = pagination.Page,
                PageSize = pagination.PageSize,
            });
    }

    public async Task<ServiceResult<IEnumerable<ReservationSummaryDto>>> GetTodayReservationsAsync(
        int? doctorId = null)
    {
        var today = DateTime.Today;
        var query = _uow.Reservations.Query()
            .Where(r => r.ReservationDate == today);
        if (doctorId.HasValue)
            query = query.Where(r => r.DoctorId == doctorId.Value);

        var summaries = await query
            .OrderBy(r => r.ReservationTime)
            .ThenBy(r => r.Id)
            .Select(r => new ReservationSummaryDto
            {
                Id = r.Id,
                PatientId = r.PatientId,
                PatientName = r.Patient != null ? $"{r.Patient.FirstName} {r.Patient.LastName}" : "",
                DoctorName = r.Doctor != null ? r.Doctor.FullName : "",
                ClinicName = r.Clinic != null ? r.Clinic.Name : "",
                TreatmentTypeName = r.TreatmentType != null ? r.TreatmentType.TypeName : "",
                ReservationDate = r.ReservationDate,
                ReservationTime = r.ReservationTime,
                StatusName = r.Status != null ? r.Status.StatusName : "Unknown",
                StatusColor = r.Status != null ? r.Status.ColorCode : "#6c757d",
                Category = r.Category.ToString(),
                IsPaid = r.IsPaid,
                HasCollectibleBalance =
                (r.QueueStatus == PatientQueueStatus.InConsultation ||
                 r.QueueStatus == PatientQueueStatus.Finished) &&
                r.Status.StatusName != "Cancelled" &&
                r.Status.StatusName != "NoShow" &&
                (r.TotalAmount ?? 0m) >
                    (r.Payments
                        .Where(p =>
                            p.Status == PaymentStatusEnum.Active ||
                            p.Status == PaymentStatusEnum.Cancelled)
                        .Sum(p => (decimal?)p.Amount) ?? 0m)
            }).ToListAsync();

        return ServiceResult<IEnumerable<ReservationSummaryDto>>.Success(summaries);
    }
    //----New Board Implementation for today reservation---//
    public async Task<ServiceResult<AppointmentBoardDto>> GetAppointmentBoardAsync(
    DateTime date,
    int? doctorId = null,
    int? clinicId = null)
    {
        var selectedDate = date.Date;

        if (selectedDate == DateTime.MaxValue.Date)
            return ServiceResult<AppointmentBoardDto>.Failure("Invalid appointment date.");

        if (doctorId.HasValue && doctorId.Value <= 0)
            return ServiceResult<AppointmentBoardDto>.Failure("Invalid doctor.");

        if (clinicId.HasValue && clinicId.Value <= 0)
            return ServiceResult<AppointmentBoardDto>.Failure("Invalid clinic.");

        var nextDate = selectedDate.AddDays(1);

        var query = _uow.Reservations
            .Query()
            .Where(r =>
                r.ReservationDate >= selectedDate &&
                r.ReservationDate < nextDate);

        if (doctorId.HasValue)
            query = query.Where(r => r.DoctorId == doctorId.Value);

        if (clinicId.HasValue)
            query = query.Where(r => r.ClinicId == clinicId.Value);

        var reservations = await query
            .OrderBy(r => r.ReservationTime)
            .ThenBy(r => r.Id)
            .Select(r => new ReservationSummaryDto
            {
                Id = r.Id,
                PatientId = r.PatientId,
                PatientName = r.Patient != null
                    ? r.Patient.FirstName + " " + r.Patient.LastName
                    : "",
                DoctorName = r.Doctor != null ? r.Doctor.FullName : "",
                ClinicName = r.Clinic != null ? r.Clinic.Name : "",
                TreatmentTypeName = r.TreatmentType != null
                    ? r.TreatmentType.TypeName
                    : "",
                ReservationDate = r.ReservationDate,
                ReservationTime = r.ReservationTime,
                StatusName = r.Status != null ? r.Status.StatusName : "Unknown",
                StatusColor = r.Status != null ? r.Status.ColorCode : "#6c757d",
                Category = r.Category.ToString(),
                IsPaid = r.IsPaid
            })
            .ToListAsync();

        return ServiceResult<AppointmentBoardDto>.Success(
            new AppointmentBoardDto
            {
                Date = selectedDate,
                DoctorId = doctorId,
                ClinicId = clinicId,
                Reservations = reservations
            });
    }
    //------------------------Queue Service Implementation----------------------------//
    public async Task<List<QueueDoctorOptionDto>> GetQueueDoctorOptionsAsync(
    int? clinicId = null)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var query = _uow.Reservations
            .Query()
            .Where(r =>
                r.ReservationDate >= today &&
                r.ReservationDate < tomorrow);

        if (clinicId.HasValue)
            query = query.Where(r => r.ClinicId == clinicId.Value);

        return await query
            .Select(r => new
            {
                r.DoctorId,
                DoctorName = r.Doctor.FullName
            })
            .Distinct()
            .OrderBy(r => r.DoctorName)
            .ThenBy(r => r.DoctorId)
            .Select(r => new QueueDoctorOptionDto
            {
                DoctorId = r.DoctorId,
                DoctorName = r.DoctorName
            })
            .ToListAsync();
    }
    public async Task<ServiceResult<List<DoctorQueueEntryDto>>> GetDoctorQueueAsync(
      int? clinicId = null,
      int? doctorId = null)
    {
        if (clinicId.HasValue && clinicId.Value <= 0)
        {
            return ServiceResult<List<DoctorQueueEntryDto>>.Failure(
                "Invalid clinic.");
        }

        if (doctorId.HasValue && doctorId.Value <= 0)
        {
            return ServiceResult<List<DoctorQueueEntryDto>>.Failure(
                "Invalid doctor.");
        }

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var query = _uow.Reservations
            .Query()
            .Where(r =>
                r.ReservationDate >= today &&
                r.ReservationDate < tomorrow &&
                r.Status.StatusName == "Confirmed" &&
                (r.QueueStatus == PatientQueueStatus.Waiting ||
                 r.QueueStatus == PatientQueueStatus.InConsultation));

        if (clinicId.HasValue)
            query = query.Where(r => r.ClinicId == clinicId.Value);

        if (doctorId.HasValue)
            query = query.Where(r => r.DoctorId == doctorId.Value);

        var entries = await query
            .OrderBy(r => r.Doctor.FullName)
            .ThenBy(r => r.DoctorId)
            .ThenBy(r =>
                r.QueueStatus == PatientQueueStatus.InConsultation ? 0 : 1)
            .ThenBy(r => r.CheckedInAtUtc ?? DateTime.MaxValue)
            .ThenBy(r => r.ReservationTime)
            .ThenBy(r => r.Id)
            .Select(r => new DoctorQueueEntryDto
            {
                ReservationId = r.Id,
                PatientId = r.PatientId,
                PatientName = r.Patient.FirstName + " " + r.Patient.LastName,

                DoctorId = r.DoctorId,
                DoctorName = r.Doctor.FullName,

                ClinicId = r.ClinicId,
                ClinicName = r.Clinic.Name,

                TreatmentTypeName = r.TreatmentType.TypeName,
                ReservationTime = r.ReservationTime,

                QueueStatus = r.QueueStatus,
                CheckedInAtUtc = r.CheckedInAtUtc,
                ConsultationStartedAtUtc = r.ConsultationStartedAtUtc
            })
            .ToListAsync();

        return ServiceResult<List<DoctorQueueEntryDto>>.Success(entries);
    }
    public async Task<ServiceResult> CheckInAsync(int reservationId)
    {
        if (reservationId <= 0)
            return ServiceResult.Failure("Invalid reservation.");
        var confirmedStatusId = await _uow.ReservationStatuses
    .Query()
    .Where(s => s.StatusName == "Confirmed")
    .Select(s => (int?)s.Id)
    .FirstOrDefaultAsync();

        if (!confirmedStatusId.HasValue)
            return ServiceResult.Failure("Confirmed reservation status is not configured.");

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var checkedInAtUtc = DateTime.UtcNow;

        var affectedRows = await _uow.Reservations
            .Query()
            .Where(r =>
                r.Id == reservationId &&
                r.ReservationDate >= today &&
                r.ReservationDate < tomorrow &&
                (r.Status.StatusName == "Pending" ||
                 r.Status.StatusName == "Confirmed") &&
                r.QueueStatus == PatientQueueStatus.NotCheckedIn &&
                r.CheckedInAtUtc == null)
              .ExecuteUpdateAsync(setters => setters
                    .SetProperty(
                        r => r.StatusId,
                        confirmedStatusId.Value)
                    .SetProperty(
                        r => r.QueueStatus,
                        PatientQueueStatus.Waiting)
                    .SetProperty(
                        r => r.CheckedInAtUtc,
                        (DateTime?)checkedInAtUtc)
                    .SetProperty(
                        r => r.UpdatedAt,
                        (DateTime?)checkedInAtUtc));

        if (affectedRows == 0)
        {
            return ServiceResult.Failure(
                "Check-in was not applied. The reservation must be for today, " +
                "Pending or Confirmed, and not already checked in. " +
                "Refresh the page to see its current state.");
        }

        return ServiceResult.Success("Patient checked in successfully.");
    }
    public async Task<ServiceResult> StartConsultationAsync(
    int reservationId,
    int currentUserId,
    bool isAdmin)
    {
        if (reservationId <= 0 || currentUserId <= 0)
            return ServiceResult.Failure("Invalid reservation or user.");

        var startedAtUtc = DateTime.UtcNow;

        var affectedRows = await _uow.Reservations
            .Query()
            .Where(r =>
                r.Id == reservationId &&
                (isAdmin || r.DoctorId == currentUserId) &&
                (r.Status.StatusName == "Confirmed") &&
                r.QueueStatus == PatientQueueStatus.Waiting &&
                r.CheckedInAtUtc != null &&
                r.ConsultationStartedAtUtc == null &&
                r.QueueEndedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    r => r.QueueStatus,
                    PatientQueueStatus.InConsultation)
                .SetProperty(
                    r => r.ConsultationStartedAtUtc,
                    (DateTime?)startedAtUtc)
                .SetProperty(
                    r => r.UpdatedAt,
                    (DateTime?)startedAtUtc));

        if (affectedRows == 0)
        {
            return ServiceResult.Failure(
                "Consultation could not be started. The patient must be waiting " +
                "with an active reservation, and you must be the assigned doctor " +
                "or an administrator. Refresh the page.");
        }

        return ServiceResult.Success("Consultation started.");
    }
    public async Task<ServiceResult> UpdateReservationAsync(
        int reservationId, UpdateReservationRequest request)
    {
        var r = await _uow.Reservations
                .Query()
                .FirstOrDefaultAsync(x => x.Id == reservationId);
        if (r is null) return ServiceResult.Failure("Reservation not found.");
        
        var isCompleted = await _uow.ReservationStatuses
                .Query()
                .AnyAsync(s => s.Id == r.StatusId && s.StatusName == "Completed");

        if (isCompleted)
            return ServiceResult.Failure("Completed reservations cannot be edited.");
        var treatmentType = await _uow.TreatmentTypes.GetByIdAsync(request.TreatmentTypeId);
        if (treatmentType is null) return ServiceResult.Failure("Treatment type not found.");

        // If the treatment type changed and the caller didn't explicitly supply a new
        // amount, re-derive the price from the new type instead of keeping the old one.
        var totalAmount = request.TotalAmount
            ?? (request.TreatmentTypeId != r.TreatmentTypeId ? treatmentType.DefaultCost : r.TotalAmount);

        var updatedAtUtc = DateTime.UtcNow;
        var reason = request.Reason?.Trim();
        var notes = request.Notes?.Trim();

        var affectedRows = await _uow.Reservations
            .Query()
            .Where(x =>
                x.Id == reservationId &&
                x.Status.StatusName != "Completed" &&
                x.StatusId == r.StatusId &&
                x.QueueStatus == r.QueueStatus &&
                x.UpdatedAt == r.UpdatedAt)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.DoctorId, request.DoctorId)
                .SetProperty(x => x.ClinicId, request.ClinicId)
                .SetProperty(x => x.TreatmentTypeId, request.TreatmentTypeId)
                .SetProperty(x => x.ReservationDate, request.ReservationDate)
                .SetProperty(x => x.ReservationTime, request.ReservationTime)
                .SetProperty(x => x.DurationMinutes, request.DurationMinutes)
                .SetProperty(x => x.Reason, reason)
                .SetProperty(x => x.Notes, notes)
                .SetProperty(x => x.TotalAmount, totalAmount)
                .SetProperty(
                    x => x.IsPaid,
                    x => totalAmount.HasValue && totalAmount.Value == 0m
                        ? true
                        : x.IsPaid)
                .SetProperty(x => x.UpdatedAt, (DateTime?)updatedAtUtc));

        if (affectedRows == 0)
        {
            return ServiceResult.Failure(
                "The reservation changed while saving or has been completed. " +
                "Reload the reservation before editing again.");
        }

        // Keep the snapshot's clinic current for the zero-cost payment below.
        r.ClinicId = request.ClinicId;

        // Same rationale as CreateReservationAsync: if this update is what made the
        // reservation free (e.g. the treatment type was changed to the nutrition
        // "Follow-up" type), record the $0 payment now so it shows up in reports too.
        // Guarded so switching back and forth doesn't create duplicate $0 rows.
        if (totalAmount.HasValue && totalAmount.Value == 0m)
        {
            var alreadyRecorded = await _uow.Payments
                .Query()
                .Where(p => p.ReservationId == r.Id && p.Status == PaymentStatusEnum.Active)
                .AnyAsync();
            if (!alreadyRecorded)
                await RecordZeroCostPaymentAsync(r, r.CreatedBy);
        }

        return ServiceResult.Success("Reservation updated.");
    }

    /// <summary>
    /// Records a $0 Payment for a reservation whose treatment type carries no charge, so
    /// the visit is genuinely visible everywhere payments are reported (dashboards,
    /// patient payment summary, etc.) instead of silently existing only as a reservation.
    /// </summary>
    private async Task RecordZeroCostPaymentAsync(Reservation reservation, int collectedBy)
    {
        var patientSourceId = await _uow.Patients
            .Query()
            .Where(p => p.Id == reservation.PatientId)
            .Select(p => p.PatientSourceId)
            .FirstOrDefaultAsync();

        var zeroPayment = new Payment
        {
            PatientId = reservation.PatientId,
            ReservationId = reservation.Id,
            ClinicId = reservation.ClinicId,
            PatientSourceId = patientSourceId,
            CollectedBy = collectedBy > 0 ? collectedBy : 1,
            Amount = 0m,
            OriginalAmount = 0m,
            PaymentMethod = PaymentMethodEnum.Cash,
            PaymentDate = DateTime.UtcNow,
            Notes = "Auto-recorded: this treatment type carries no charge.",
            CreatedAt = DateTime.UtcNow,
            SourceDeductionAmount = 0m,
            ClinicNetAmount = 0m,
            Status = PaymentStatusEnum.Active
        };
        await _uow.Payments.AddAsync(zeroPayment);
        await _uow.SaveChangesAsync();
    }

    public async Task<ServiceResult<ReservationDto>> UpdateStatusAsync(
    int reservationId,
    int statusId)
    {
        var targetStatus = await _uow.ReservationStatuses
            .Query()
            .Where(s => s.Id == statusId)
            .Select(s => s.StatusName)
            .FirstOrDefaultAsync();

        if (targetStatus is null)
            return ServiceResult<ReservationDto>.Failure("Invalid status.");

        var isCompleted = targetStatus == "Completed";
        var isCancelledOrNoShow =
            targetStatus == "Cancelled" || targetStatus == "NoShow";

        var closesQueue = isCompleted || isCancelledOrNoShow;
        var isPending = targetStatus == "Pending";
        var isActiveStatus = isPending || targetStatus == "Confirmed";
        var updatedAtUtc = DateTime.UtcNow;

        var affectedRows = await _uow.Reservations
            .Query()
            .Where(r =>
                        r.Id == reservationId &&
                        r.Status.StatusName != "Completed" &&
                        (!isCompleted ||
                         (r.Status.StatusName == "Confirmed" &&
                          r.QueueStatus == PatientQueueStatus.InConsultation &&
                          r.ConsultationStartedAtUtc != null &&
                          r.QueueEndedAtUtc == null)) &&
                        (!isPending ||
                         (r.QueueStatus != PatientQueueStatus.Waiting &&
                          r.QueueStatus != PatientQueueStatus.InConsultation)) &&
                        (!isActiveStatus ||
                         (r.QueueStatus != PatientQueueStatus.Finished &&
                          r.QueueStatus != PatientQueueStatus.Left)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    r => r.StatusId,
                    statusId)
                .SetProperty(
                    r => r.QueueEndedAtUtc,
                    r => closesQueue &&
                         (r.QueueStatus == PatientQueueStatus.Waiting ||
                          r.QueueStatus == PatientQueueStatus.InConsultation)
                        ? r.QueueEndedAtUtc ?? (DateTime?)updatedAtUtc
                        : r.QueueEndedAtUtc)
                .SetProperty(
                    r => r.QueueStatus,
                    r => closesQueue &&
                         (r.QueueStatus == PatientQueueStatus.Waiting ||
                          r.QueueStatus == PatientQueueStatus.InConsultation)
                        ? (isCompleted
                            ? PatientQueueStatus.Finished
                            : PatientQueueStatus.Left)
                        : r.QueueStatus)
                .SetProperty(
                    r => r.UpdatedAt,
                    (DateTime?)updatedAtUtc));

        if (affectedRows == 0)
        {
            return ServiceResult<ReservationDto>.Failure(
                isCompleted
                    ? "Completion was not applied. The reservation must be Confirmed " +
                      "and its queue must be In Consultation with a recorded start " +
                      "time and no end time."
                    : "Status was not changed. The reservation may be missing or completed, " +
                      "or the requested status conflicts with its queue state. " +
                      "Refresh the page.");
        }

        var reservation = await GetReservationDtoByIdAsync(reservationId);

        if (reservation is null)
        {
            return ServiceResult<ReservationDto>.Failure(
                "Status changed, but the reservation could not be reloaded.");
        }

        return ServiceResult<ReservationDto>.Success(reservation);
    }

    public async Task<ServiceResult> MarkAsPaidAsync(int reservationId)
    {
        var updatedAt = DateTime.UtcNow;

        var affectedRows = await _uow.Reservations
            .Query()
            .Where(r => r.Id == reservationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.IsPaid, true)
                .SetProperty(r => r.UpdatedAt, updatedAt));

        return affectedRows == 0
            ? ServiceResult.Failure("Reservation not found.")
            : ServiceResult.Success();
    }

    public async Task<ServiceResult> CancelReservationAsync(
    int reservationId,
    string? reason = null)
    {
        var cancelledStatusId = await _uow.ReservationStatuses
            .Query()
            .Where(s => s.StatusName == "Cancelled")
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync();

        if (!cancelledStatusId.HasValue)
            return ServiceResult.Failure(
                "The Cancelled reservation status is not configured.");

        var updatedAtUtc = DateTime.UtcNow;
        var cancellationNote = string.IsNullOrWhiteSpace(reason)
            ? null
            : $" | Cancelled: {reason.Trim()}";

        var affectedRows = await _uow.Reservations
            .Query()
            .Where(r =>
                r.Id == reservationId &&
                r.Status.StatusName != "Completed" &&
                r.Status.StatusName != "Cancelled" &&
                r.QueueStatus != PatientQueueStatus.Finished)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    r => r.StatusId,
                    cancelledStatusId.Value)
                .SetProperty(
                    r => r.QueueEndedAtUtc,
                    r => r.QueueStatus == PatientQueueStatus.Waiting ||
                         r.QueueStatus == PatientQueueStatus.InConsultation
                        ? r.QueueEndedAtUtc ?? (DateTime?)updatedAtUtc
                        : r.QueueEndedAtUtc)
                .SetProperty(
                    r => r.QueueStatus,
                    r => r.QueueStatus == PatientQueueStatus.Waiting ||
                         r.QueueStatus == PatientQueueStatus.InConsultation
                        ? PatientQueueStatus.Left
                        : r.QueueStatus)
                .SetProperty(
                    r => r.Notes,
                    r => cancellationNote == null
                        ? r.Notes
                        : (r.Notes ?? string.Empty) + cancellationNote)
                .SetProperty(
                    r => r.UpdatedAt,
                    (DateTime?)updatedAtUtc));

        if (affectedRows == 0)
        {
            return ServiceResult.Failure(
                "Cancellation was not applied. The reservation may be missing, " +
                "already cancelled, completed, or its queue may be finished. " +
                "Refresh the page.");
        }

        return ServiceResult.Success("Reservation cancelled.");
    }

    public async Task<ServiceResult<IEnumerable<TreatmentTypeDto>>> GetTreatmentTypesAsync(
    string? category = null)
    {
        var query = _uow.TreatmentTypes
            .Query()
            .Where(t => t.IsActive);

        if (!string.IsNullOrWhiteSpace(category) &&
            Enum.TryParse<TreatmentCategory>(category, out var cat))
        {
            query = query.Where(t => t.Category == cat);
        }

        var types = await query
            .Select(t => new TreatmentTypeDto
            {
                Id = t.Id,
                Category = t.Category.ToString(),
                TypeName = t.TypeName,
                Description = t.Description,
                DefaultCost = t.DefaultCost,
                DurationMinutes = t.DurationMinutes,
                IsActive = t.IsActive
            })
            .ToListAsync();

        return ServiceResult<IEnumerable<TreatmentTypeDto>>.Success(types);
    }
    //----Consultation Service Implementation----//
    public async Task<ServiceResult<ConsultationContextDto>>
    GetConsultationContextAsync(
        int reservationId,
        int currentUserId,
        bool isAdmin)
    {
        if (reservationId <= 0 || currentUserId <= 0)
        {
            return ServiceResult<ConsultationContextDto>.Failure(
                "Invalid reservation or user.");
        }

        var reservation = await _uow.Reservations
            .Query()
            .Where(r =>
                r.Id == reservationId &&
                (isAdmin || r.DoctorId == currentUserId))
            .Select(r => new
            {
                r.Id,
                r.PatientId,
                r.DoctorId,
                r.Category,
                r.QueueStatus,
                StatusName = r.Status.StatusName,
                PatientRecordId = r.PatientRecord != null
                    ? (int?)r.PatientRecord.Id
                    : null
            })
            .FirstOrDefaultAsync();

        if (reservation is null)
        {
            return ServiceResult<ConsultationContextDto>.Failure(
                "Reservation not found or you are not its assigned doctor.");
        }

        if (reservation.StatusName != "Confirmed" ||
            reservation.QueueStatus != PatientQueueStatus.InConsultation)
        {
            return ServiceResult<ConsultationContextDto>.Failure(
                "The reservation must be in consultation before opening this workspace.");
        }

        return ServiceResult<ConsultationContextDto>.Success(
            new ConsultationContextDto
            {
                ReservationId = reservation.Id,
                PatientId = reservation.PatientId,
                DoctorId = reservation.DoctorId,
                Category = reservation.Category.ToString(),
                PatientRecordId = reservation.PatientRecordId
            });
    }
    public async Task<ServiceResult<ReservationDto>> CompleteConsultationAsync(
    int recordId,
    int currentUserId,
    bool isAdmin)
    {
        if (recordId <= 0 || currentUserId <= 0)
        {
            return ServiceResult<ReservationDto>.Failure(
                "Invalid record or user.");
        }

        var reservationId = await _uow.PatientRecords
            .Query()
            .Where(r => r.Id == recordId)
            .Select(r => r.ReservationId)
            .FirstOrDefaultAsync();

        if (!reservationId.HasValue)
        {
            return ServiceResult<ReservationDto>.Failure(
                "This record is not linked to a reservation.");
        }

        var completedStatusId = await _uow.ReservationStatuses
            .Query()
            .Where(s => s.StatusName == "Completed")
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync();

        if (!completedStatusId.HasValue)
        {
            return ServiceResult<ReservationDto>.Failure(
                "The Completed reservation status is not configured.");
        }

        var completedAtUtc = DateTime.UtcNow;

        var affectedRows = await _uow.Reservations
            .Query()
            .Where(r =>
                r.Id == reservationId.Value &&
                (isAdmin || r.DoctorId == currentUserId) &&
                r.Status.StatusName == "Confirmed" &&
                r.QueueStatus == PatientQueueStatus.InConsultation &&
                r.ConsultationStartedAtUtc != null &&
                r.QueueEndedAtUtc == null &&
                r.PatientRecord != null &&
                r.PatientRecord.Id == recordId &&
                r.PatientRecord.PatientId == r.PatientId &&
                r.PatientRecord.DoctorId == r.DoctorId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.StatusId, completedStatusId.Value)
                .SetProperty(
                    r => r.QueueStatus,
                    PatientQueueStatus.Finished)
                .SetProperty(
                    r => r.QueueEndedAtUtc,
                    (DateTime?)completedAtUtc)
                .SetProperty(
                    r => r.UpdatedAt,
                    (DateTime?)completedAtUtc));

        if (affectedRows == 0)
        {
            return ServiceResult<ReservationDto>.Failure(
                "Consultation was not completed. You must be the assigned " +
                "doctor or an administrator, and the reservation must still " +
                "be in consultation with its matching medical record. " +
                "Refresh the page.");
        }

        var reservation = await GetReservationDtoByIdAsync(
            reservationId.Value);

        if (reservation is null)
        {
            return ServiceResult<ReservationDto>.Failure(
                "Consultation was completed, but the reservation could not be reloaded.");
        }

        return ServiceResult<ReservationDto>.Success(reservation);
    }
    // ── Mappers ──────────────────────────────────────────────
    private async Task<ReservationDto?> GetReservationDtoByIdAsync(int reservationId)
    {
        return await _uow.Reservations.Query()
            .Where(r => r.Id == reservationId)
            .Select(r => new ReservationDto
            {
                Id = r.Id,
                StatusId = r.StatusId,
                PatientId = r.PatientId,
                PatientName = r.Patient != null
                    ? $"{r.Patient.FirstName} {r.Patient.LastName}"
                    : "",

                DoctorId = r.DoctorId,
                DoctorName = r.Doctor != null
                    ? r.Doctor.FullName
                    : "",

                ClinicId = r.ClinicId,
                ClinicName = r.Clinic != null
                    ? r.Clinic.Name
                    : "",

                TreatmentTypeId = r.TreatmentTypeId,
                TreatmentTypeName = r.TreatmentType != null
                    ? r.TreatmentType.TypeName
                    : "",

                StatusName = r.Status != null
                    ? r.Status.StatusName
                    : "",

                StatusColor = r.Status != null
                    ? r.Status.ColorCode
                    : "#6c757d",

                Category = r.Category.ToString(),

                ReservationDate = r.ReservationDate,
                ReservationTime = r.ReservationTime,
                DurationMinutes = r.DurationMinutes,

                Reason = r.Reason,
                Notes = r.Notes,

                IsPaid = r.IsPaid,
                TotalAmount = r.TotalAmount,

                CreatedAt = r.CreatedAt,
                QueueStatus = r.QueueStatus,
                CheckedInAtUtc = r.CheckedInAtUtc,
                ConsultationStartedAtUtc = r.ConsultationStartedAtUtc
            })
            .FirstOrDefaultAsync();
    }
      //--------------------------------------//
    public async Task<ServiceResult<List<ReservationDto>>> GetPatientReservationHistoryAsync(int patientId)
    {
        try
        {
            var reservations = await _uow.Reservations.Query()
                .Where(r => r.PatientId == patientId)
                .OrderByDescending(r => r.ReservationDate)
                .ThenByDescending(r => r.ReservationTime)
                .ThenByDescending(r => r.Id)
                .Select(r => new ReservationDto
                {
                    Id = r.Id,
                    PatientId = r.PatientId,
                    DoctorId = r.DoctorId,
                    ClinicId = r.ClinicId,
                    StatusId = r.StatusId,
                    TreatmentTypeId = r.TreatmentTypeId,

                    ReservationDate = r.ReservationDate,
                    ReservationTime = r.ReservationTime,
                    Category = r.Category.ToString(),
                    IsPaid = r.IsPaid,

                    PatientName = r.Patient != null
                        ? $"{r.Patient.FirstName} {r.Patient.LastName}"
                        : "",

                    DoctorName = r.Doctor != null
                        ? r.Doctor.FullName
                        : "",

                    ClinicName = r.Clinic != null
                        ? r.Clinic.Name
                        : "",

                    StatusName = r.Status != null
                        ? r.Status.StatusName
                        : "Unknown",

                    StatusColor = r.Status != null
                        ? r.Status.ColorCode
                        : "#6c757d",

                    TreatmentTypeName = r.TreatmentType != null
                        ? r.TreatmentType.TypeName
                        : "",
                    QueueStatus = r.QueueStatus,
                    CheckedInAtUtc = r.CheckedInAtUtc,
                    ConsultationStartedAtUtc = r.ConsultationStartedAtUtc
                })
                .ToListAsync();

            return ServiceResult<List<ReservationDto>>.Success(reservations);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<ReservationDto>>.Failure(
                $"حدث خطأ أثناء جلب سجل الحجوزات: {ex.Message}");
        }
    }
}
