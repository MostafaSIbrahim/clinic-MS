using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.MedicalRecord;
using SafyaClinic.Application.DTOs.Reservation;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Domain.Entities.MedicalRecord;
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
            IsPaid = false,
            TotalAmount = totalAmount,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        await _uow.Reservations.AddAsync(reservation);
        await _uow.SaveChangesAsync();
        return ServiceResult<ReservationDto>.Success(await BuildReservationDtoAsync(reservation));
    }

    public async Task<ServiceResult<ReservationDto>> GetReservationByIdAsync(int reservationId)
    {
        var reservation = await _uow.Reservations.GetByIdAsync(reservationId);
        if (reservation is null)
            return ServiceResult<ReservationDto>.Failure("Reservation not found.");

        return ServiceResult<ReservationDto>.Success(await BuildReservationDtoAsync(reservation));
    }

    public async Task<ServiceResult<PagedResult<ReservationSummaryDto>>> GetReservationsAsync(
        ReservationFilterRequest filter, PaginationRequest pagination)
    {
        var all = await _uow.Reservations.GetAllAsync();

        if (filter.DoctorId.HasValue)
            all = all.Where(r => r.DoctorId == filter.DoctorId.Value);
        if (filter.PatientId.HasValue)
            all = all.Where(r => r.PatientId == filter.PatientId.Value);
        if (filter.ClinicId.HasValue)
            all = all.Where(r => r.ClinicId == filter.ClinicId.Value);
        if (filter.StatusId.HasValue)
            all = all.Where(r => r.StatusId == filter.StatusId.Value);
        if (!string.IsNullOrWhiteSpace(filter.Category) &&
            Enum.TryParse<TreatmentCategory>(filter.Category, out var cat))
            all = all.Where(r => r.Category == cat);
        if (filter.DateFrom.HasValue)
            all = all.Where(r => r.ReservationDate >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue)
            all = all.Where(r => r.ReservationDate <= filter.DateTo.Value);
        if (filter.IsPaid.HasValue)
            all = all.Where(r => r.IsPaid == filter.IsPaid.Value);

        all = all.OrderByDescending(r => r.ReservationDate).ThenBy(r => r.ReservationTime);

        var totalCount = all.Count();
        var paged = all.Skip((pagination.Page - 1) * pagination.PageSize)
                       .Take(pagination.PageSize).ToList();

        var summaries = new List<ReservationSummaryDto>();
        foreach (var r in paged)
            summaries.Add(await BuildReservationSummaryAsync(r));

        return ServiceResult<PagedResult<ReservationSummaryDto>>.Success(
            new PagedResult<ReservationSummaryDto>
            {
                Items = summaries,
                TotalCount = totalCount,
                Page = pagination.Page,
                PageSize = pagination.PageSize
            });
    }

    public async Task<ServiceResult<IEnumerable<ReservationSummaryDto>>> GetTodayReservationsAsync(
        int? doctorId = null)
    {
        var today = DateTime.Today;
        var all = await _uow.Reservations.FindAsync(r => r.ReservationDate == today);
        if (doctorId.HasValue)
            all = all.Where(r => r.DoctorId == doctorId.Value);

        all = all.OrderBy(r => r.ReservationTime);

        var summaries = new List<ReservationSummaryDto>();
        foreach (var r in all)
            summaries.Add(await BuildReservationSummaryAsync(r));

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
        r.UpdatedAt = DateTime.UtcNow;

        _uow.Reservations.Update(r);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Reservation updated.");
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
        return ServiceResult<ReservationDto>.Success(await BuildReservationDtoAsync(r));
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

    private async Task<ReservationDto> BuildReservationDtoAsync(Reservation r)
    {
        var patient = await _uow.Patients.GetByIdAsync(r.PatientId);
        var doctor = await _uow.Users.GetByIdAsync(r.DoctorId);
        var clinic = await _uow.Clinics.GetByIdAsync(r.ClinicId);
        var status = await _uow.ReservationStatuses.GetByIdAsync(r.StatusId);
        var treatmentType = await _uow.TreatmentTypes.GetByIdAsync(r.TreatmentTypeId);

        return new ReservationDto
        {
            Id = r.Id,
            PatientId = r.PatientId,
            PatientName = patient is null ? "" : $"{patient.FirstName} {patient.LastName}",
            DoctorId = r.DoctorId,
            DoctorName = doctor?.FullName ?? "",
            ClinicId = r.ClinicId,
            ClinicName = clinic?.Name ?? "",
            TreatmentTypeId = r.TreatmentTypeId,
            TreatmentTypeName = treatmentType?.TypeName ?? "",
            StatusName = status?.StatusName ?? "",
            StatusColor = status?.ColorCode ?? "#6c757d",
            Category = r.Category.ToString(),
            ReservationDate = r.ReservationDate,
            ReservationTime = r.ReservationTime,
            DurationMinutes = r.DurationMinutes,
            Reason = r.Reason,
            Notes = r.Notes,
            IsPaid = r.IsPaid,
            TotalAmount = r.TotalAmount,
            CreatedAt = r.CreatedAt
        };
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
            var all = await _uow.Reservations.GetAllAsync();
            var patientReservations = (all ?? Enumerable.Empty<Reservation>())
                .Where(r => r != null && r.PatientId == patientId)
                .OrderByDescending(r => r.ReservationDate)
                .ThenByDescending(r => r.ReservationTime)
                .ToList();
                        
            var dtos = new List<ReservationDto>();
            foreach (var r in patientReservations)
                dtos.Add(await BuildReservationDtoAsync(r));

            return ServiceResult<List<ReservationDto>>.Success(dtos);
        }
        catch (Exception ex)
        {
            return ServiceResult<List<ReservationDto>>.Failure($"حدث خطأ أثناء جلب سجل الحجوزات: {ex.Message}");
        }
    }
}
