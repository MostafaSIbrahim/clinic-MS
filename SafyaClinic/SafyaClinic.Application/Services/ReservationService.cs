using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Reservation;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Domain.Entities.Reservation;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Interfaces.Repositories;

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
        if (!Enum.TryParse<TreatmentCategory>(request.Category, out var category))
            return ServiceResult<ReservationDto>.Failure("Invalid category. Use 'InternalMedicine' or 'Nutritional'.");

        // Default status = Pending (ID 1)
        var reservation = new Reservation
        {
            PatientId = request.PatientId,
            DoctorId = request.DoctorId,
            StatusId = 1,
            Category = category,
            ReservationDate = request.ReservationDate,
            ReservationTime = request.ReservationTime,
            DurationMinutes = request.DurationMinutes,
            Reason = request.Reason?.Trim(),
            Notes = request.Notes?.Trim(),
            IsPaid = false,
            TotalAmount = request.TotalAmount,
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

        r.DoctorId = request.DoctorId;
        r.StatusId = request.StatusId;
        r.ReservationDate = request.ReservationDate;
        r.ReservationTime = request.ReservationTime;
        r.DurationMinutes = request.DurationMinutes;
        r.Reason = request.Reason?.Trim();
        r.Notes = request.Notes?.Trim();
        r.TotalAmount = request.TotalAmount;
        r.UpdatedAt = DateTime.UtcNow;

        _uow.Reservations.Update(r);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Reservation updated.");
    }

    public async Task<ServiceResult> UpdateStatusAsync(int reservationId, int statusId)
    {
        var r = await _uow.Reservations.GetByIdAsync(reservationId);
        if (r is null) return ServiceResult.Failure("Reservation not found.");
        if (!await _uow.ReservationStatuses.ExistsAsync(statusId))
            return ServiceResult.Failure("Invalid status.");

        r.StatusId = statusId;
        r.UpdatedAt = DateTime.UtcNow;
        _uow.Reservations.Update(r);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success();
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

    // ── Mappers ──────────────────────────────────────────────

    private async Task<ReservationDto> BuildReservationDtoAsync(Reservation r)
    {
        var patient = await _uow.Patients.GetByIdAsync(r.PatientId);
        var doctor = await _uow.Users.GetByIdAsync(r.DoctorId);
        var status = await _uow.ReservationStatuses.GetByIdAsync(r.StatusId);

        return new ReservationDto
        {
            Id = r.Id,
            PatientId = r.PatientId,
            PatientName = patient is null ? "" : $"{patient.FirstName} {patient.LastName}",
            DoctorId = r.DoctorId,
            DoctorName = doctor?.FullName ?? "",
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
        var status = await _uow.ReservationStatuses.GetByIdAsync(r.StatusId);

        return new ReservationSummaryDto
        {
            Id = r.Id,
            PatientName = patient is null ? "" : $"{patient.FirstName} {patient.LastName}",
            DoctorName = doctor?.FullName ?? "",
            ReservationDate = r.ReservationDate,
            ReservationTime = r.ReservationTime,
            StatusName = status?.StatusName ?? "",
            StatusColor = status?.ColorCode ?? "#6c757d",
            Category = r.Category.ToString(),
            IsPaid = r.IsPaid
        };
    }
}
