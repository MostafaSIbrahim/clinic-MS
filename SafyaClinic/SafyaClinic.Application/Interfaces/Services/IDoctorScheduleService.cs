using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.Scheduling;

namespace SafyaClinic.Application.Interfaces.Services;

/// <summary>P1: doctors' calendars per clinic.</summary>
public interface IDoctorScheduleService
{
    /// <summary>Every active clinician in every active clinic, with their schedule if set. Optionally one doctor only.</summary>
    Task<ServiceResult<IReadOnlyList<ScheduleSummaryDto>>> ListAsync(int? doctorId = null);

    /// <summary>The doctor's calendar in the clinic, or a new open calendar with a 15-minute step if none is set.</summary>
    Task<ServiceResult<DoctorScheduleView>> GetAsync(int doctorId, int clinicId);

    /// <summary>Creates or replaces the doctor's calendar in the clinic. Nothing changes if any rule fails or a future booking would be stranded.</summary>
    Task<ServiceResult> SaveAsync(DoctorScheduleDto schedule, int actingUserId);

    /// <summary>P1c: sets a date's change (day off, or that date's own periods), replacing any change on that date.</summary>
    Task<ServiceResult> SetDateChangeAsync(int doctorId, int clinicId, DateChangeDto change, int actingUserId);

    /// <summary>P1c: removes a date's change, so the weekly calendar applies again.</summary>
    Task<ServiceResult> RemoveDateChangeAsync(int doctorId, int clinicId, DateTime date, int actingUserId);

    /// <summary>P1c: free start times for a visit of this length on a date (no past times; the doctor's bookings in every clinic count).</summary>
    Task<ServiceResult<IReadOnlyList<TimeSpan>>> FreeStartsAsync(int doctorId, int clinicId, DateTime date, int visitMinutes);
}
