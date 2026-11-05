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

    /// <summary>Creates or replaces the doctor's calendar in the clinic. Nothing changes if any rule fails.</summary>
    Task<ServiceResult> SaveAsync(DoctorScheduleDto schedule, int actingUserId);
}
