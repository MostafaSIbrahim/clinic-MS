using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.Scheduling;

namespace SafyaClinic.Application.Interfaces.Services;

/// <summary>P1: each clinic's weekly opening hours.</summary>
public interface IClinicHoursService
{
    /// <summary>The clinic's periods, Saturday first, each day by start time. Empty when none are set.</summary>
    Task<ServiceResult<IReadOnlyList<OpeningPeriodDto>>> GetAsync(int clinicId);

    /// <summary>Replaces the clinic's whole week. Nothing changes if any period is invalid.</summary>
    Task<ServiceResult> SaveAsync(int clinicId, IReadOnlyList<OpeningPeriodDto> periods, int actingUserId);
}
