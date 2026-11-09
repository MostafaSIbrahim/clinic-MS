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
    Task<ServiceResult> UpdateReservationAsync(int reservationId, UpdateReservationRequest request, int updatedBy);

    // Treatment types (used when booking a reservation to derive the price)
    Task<ServiceResult<IEnumerable<TreatmentTypeDto>>> GetTreatmentTypesAsync(string? category = null);

    /// <summary>P2c: moves an upcoming booking to another free time of the same doctor (optionally another clinic).</summary>
    Task<ServiceResult> MoveReservationAsync(int reservationId, int clinicId, DateTime date, TimeSpan time, int actingUserId);
    Task<ServiceResult<List<ReservationDto>>> GetPatientReservationHistoryAsync(int patientId);
    Task<ServiceResult<AppointmentBoardDto>> GetAppointmentBoardAsync(
    DateTime date,
    int? doctorId = null,
    int? clinicId = null);
    Task<ServiceResult> CheckInAsync(int reservationId);
    Task<ServiceResult> StartConsultationAsync(
    int reservationId,
    int currentUserId,
    bool isAdmin);
    /// <summary>Today's Pending or Confirmed bookings that haven't been checked in yet (the queue's "Not arrived yet").</summary>
    Task<List<DoctorQueueEntryDto>> GetExpectedArrivalsAsync(int? clinicId = null, int? doctorId = null);

    Task<ServiceResult<List<DoctorQueueEntryDto>>> GetDoctorQueueAsync(
    int? clinicId = null,
    int? doctorId = null);
    Task<List<QueueDoctorOptionDto>> GetQueueDoctorOptionsAsync(
    int? clinicId = null);
    Task<ServiceResult<ConsultationContextDto>> GetConsultationContextAsync(
    int reservationId,
    int currentUserId,
    bool isAdmin);
    /// <summary>P5c: completes the visit documented in this record (the given one, or the one in consultation).</summary>
    Task<ServiceResult<ReservationDto>> CompleteConsultationAsync(
    int recordId,
    int currentUserId,
    bool isAdmin,
    int? reservationId = null);

    /// <summary>P5c: the visit documented in this record that is in consultation now, if any.</summary>
    Task<int?> GetVisitInConsultationAsync(int recordId);
    Task<ServiceResult<ReservationDto>> BookFollowUpAsync(
    int recordId,
    CreateReservationRequest request,
    int currentUserId,
    bool canManageAll);
    Task<ServiceResult<ReservationDto>> UpdateStatusAsync(
    int reservationId,
    int statusId,
    int currentUserId);

    Task<ServiceResult> CancelReservationAsync(
        int reservationId,
        int currentUserId,
        string? reason = null);
}