using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.Scheduling;

namespace SafyaClinic.Application.Interfaces.Services;

/// <summary>P2b: the appointments screen.</summary>
public interface IAppointmentsService
{
    /// <summary>
    /// A clinic's day: each clinician who works there that day (or has bookings there) with their bookings and
    /// their free start times for a visit of this length; the other clinicians by name.
    /// </summary>
    Task<ServiceResult<AppointmentDayDto>> GetDayAsync(DateTime date, int clinicId, int visitMinutes);

    /// <summary>P2c: a booking and the doctor's free times for it on a date in a clinic (the booking's own time doesn't count as taken).</summary>
    Task<ServiceResult<MoveOptionsDto>> GetMoveAsync(int reservationId, DateTime? date, int? clinicId);
}
