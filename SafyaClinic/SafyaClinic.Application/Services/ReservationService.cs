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
        if (!await _uow.Patients.ExistsAsync(request.PatientId))
            return ServiceResult<ReservationDto>.Failure("Patient not found.");
        if (!await _uow.Users.ExistsAsync(request.DoctorId))
            return ServiceResult<ReservationDto>.Failure("Doctor not found.");
        if (!await _uow.Clinics.ExistsAsync(request.ClinicId))
            return ServiceResult<ReservationDto>.Failure("Clinic not found.");
        if (!Enum.TryParse<TreatmentCategory>(request.Category, out var category))
            return ServiceResult<ReservationDto>.Failure("Invalid category. Use 'InternalMedicine' or 'Nutritional'.");

        var treatmentType = await _uow.TreatmentTypes.GetByIdAsync(request.TreatmentTypeId);
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
                IsPaid = r.IsPaid
            }).ToListAsync();

        return ServiceResult<IEnumerable<ReservationSummaryDto>>.Success(summaries);
    }

    public async Task<ServiceResult> UpdateReservationAsync(
        int reservationId, UpdateReservationRequest request)
    {
        var r = await _uow.Reservations.GetByIdAsync(reservationId);
        if (r is null) return ServiceResult.Failure("Reservation not found.");

        var treatmentType = await _uow.TreatmentTypes.GetByIdAsync(request.TreatmentTypeId);
        if (treatmentType is null) return ServiceResult.Failure("Treatment type not found.");

        // If the treatment type changed and the caller didn't explicitly supply a new
        // amount, re-derive the price from the new type instead of keeping the old one.
        var totalAmount = request.TotalAmount
            ?? (request.TreatmentTypeId != r.TreatmentTypeId ? treatmentType.DefaultCost : r.TotalAmount);

        r.DoctorId = request.DoctorId;
        r.ClinicId = request.ClinicId;
        r.TreatmentTypeId = request.TreatmentTypeId;
        r.StatusId = request.StatusId;
        r.ReservationDate = request.ReservationDate;
        r.ReservationTime = request.ReservationTime;
        r.DurationMinutes = request.DurationMinutes;
        r.Reason = request.Reason?.Trim();
        r.Notes = request.Notes?.Trim();
        r.TotalAmount = totalAmount;

        // If the (possibly new) treatment type carries no charge, there's nothing left to
        // collect — mark it paid automatically, same as at creation time. Otherwise leave
        // the existing IsPaid flag alone; it's kept in sync with real payments separately
        // via PaymentService.RecalculateReservationPaidStatusAsync.
        if (totalAmount.HasValue && totalAmount.Value == 0m)
            r.IsPaid = true;

        r.UpdatedAt = DateTime.UtcNow;

        _uow.Reservations.Update(r);
        await _uow.SaveChangesAsync();

        // Same rationale as CreateReservationAsync: if this update is what made the
        // reservation free (e.g. the treatment type was changed to the nutrition
        // "Follow-up" type), record the $0 payment now so it shows up in reports too.
        // Guarded so switching back and forth doesn't create duplicate $0 rows.
        if (totalAmount.HasValue && totalAmount.Value == 0m)
        {
            var alreadyRecorded = (await _uow.Payments.FindAsync(
                p => p.ReservationId == r.Id && p.Status == PaymentStatusEnum.Active)).Any();
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
        var patient = await _uow.Patients.GetByIdAsync(reservation.PatientId);

        var zeroPayment = new Payment
        {
            PatientId = reservation.PatientId,
            ReservationId = reservation.Id,
            ClinicId = reservation.ClinicId,
            PatientSourceId = patient?.PatientSourceId,
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

    public async Task<ServiceResult<ReservationDto>> UpdateStatusAsync(int reservationId, int statusId)
    {
        var r = await _uow.Reservations.GetByIdAsync(reservationId);
        if (r is null) return ServiceResult<ReservationDto>.Failure("Reservation not found.");
        if (!await _uow.ReservationStatuses.ExistsAsync(statusId))
            return ServiceResult<ReservationDto>.Failure("Invalid status.");
        if (r.StatusId == 3) // Completed — status is locked once the visit is done
            return ServiceResult<ReservationDto>.Failure("This reservation is completed and its status can no longer be changed.");

        r.StatusId = statusId;
        r.UpdatedAt = DateTime.UtcNow;
        _uow.Reservations.Update(r);
        await _uow.SaveChangesAsync();
        var reservation = await GetReservationDtoByIdAsync(r.Id);
        if (reservation is null)
            return ServiceResult<ReservationDto>.Failure("Failed to retrieve updated reservation.");

        return ServiceResult<ReservationDto>.Success(reservation);
    }

    public async Task<ServiceResult> MarkAsPaidAsync(int reservationId)
    {
        var r = await _uow.Reservations.GetByIdAsync(reservationId);
        if (r is null) return ServiceResult.Failure("Reservation not found.");

        r.IsPaid = true;
        r.UpdatedAt = DateTime.UtcNow;
        _uow.Reservations.Update(r);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> CancelReservationAsync(int reservationId, string? reason = null)
    {
        var r = await _uow.Reservations.GetByIdAsync(reservationId);
        if (r is null) return ServiceResult.Failure("Reservation not found.");

        r.StatusId = 4; // Cancelled
        r.Notes = string.IsNullOrWhiteSpace(reason) ? r.Notes : $"{r.Notes} | Cancelled: {reason}";
        r.UpdatedAt = DateTime.UtcNow;
        _uow.Reservations.Update(r);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Reservation cancelled.");
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

    // ── Mappers ──────────────────────────────────────────────
    private async Task<ReservationDto?> GetReservationDtoByIdAsync(int reservationId)
    {
        return await _uow.Reservations.Query()
            .Where(r => r.Id == reservationId)
            .Select(r => new ReservationDto
            {
                Id = r.Id,

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

                CreatedAt = r.CreatedAt
            })
            .FirstOrDefaultAsync();
    }
  
    private async Task<ReservationSummaryDto> BuildReservationSummaryAsync(Reservation r)
    {
        var patient = await _uow.Patients.GetByIdAsync(r.PatientId);
        var doctor = await _uow.Users.GetByIdAsync(r.DoctorId);
        var clinic = await _uow.Clinics.GetByIdAsync(r.ClinicId);
        var status = await _uow.ReservationStatuses.GetByIdAsync(r.StatusId);
        var treatmentType = await _uow.TreatmentTypes.GetByIdAsync(r.TreatmentTypeId);

        return new ReservationSummaryDto
        {
            Id = r.Id,
            PatientId = r.PatientId,
            PatientName = patient is null ? "" : $"{patient.FirstName} {patient.LastName}",
            DoctorName = doctor?.FullName ?? "",
            ClinicName = clinic?.Name ?? "",
            TreatmentTypeName = treatmentType?.TypeName ?? "",
            ReservationDate = r.ReservationDate,
            ReservationTime = r.ReservationTime,
            StatusName = status?.StatusName ?? "",
            StatusColor = status?.ColorCode ?? "#6c757d",
            Category = r.Category.ToString(),
            IsPaid = r.IsPaid
        };
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
                        : ""
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
