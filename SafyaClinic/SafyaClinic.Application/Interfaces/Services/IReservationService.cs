using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.MedicalRecord;
using SafyaClinic.Application.DTOs.Reservation;

namespace SafyaClinic.Application.Interfaces.Services;

public interface IReservationService
{
    Task<ServiceResult<ReservationDto>> CreateReservationAsync(CreateReservationRequest request, int createdBy);
    Task<ServiceResult<ReservationDto>> GetReservationByIdAsync(int reservationId);
    Task<ServiceResult<PagedResult<ReservationSummaryDto>>> GetReservationsAsync(ReservationFilterRequest filter, PaginationRequest pagination);
    Task<ServiceResult<IEnumerable<ReservationSummaryDto>>> GetTodayReservationsAsync(int? doctorId = null);
    Task<ServiceResult> UpdateReservationAsync(int reservationId, UpdateReservationRequest request);
    Task<ServiceResult<ReservationDto>> UpdateStatusAsync(int reservationId, int statusId);
    Task<ServiceResult> MarkAsPaidAsync(int reservationId);
    Task<ServiceResult> CancelReservationAsync(int reservationId, string? reason = null);

    // Treatment types (used when booking a reservation to derive the price)
    Task<ServiceResult<IEnumerable<TreatmentTypeDto>>> GetTreatmentTypesAsync(string? category = null);
}