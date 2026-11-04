using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Common.Auditing;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.Scheduling;
using SafyaClinic.Domain.Entities.Settings;
using SafyaClinic.Domain.Interfaces.Repositories;

namespace SafyaClinic.Application.Services;

/// <summary>P1: reads and saves clinic opening hours; every change is audited.</summary>
public class ClinicHoursService : IClinicHoursService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditTrail? _audit;

    public ClinicHoursService(IUnitOfWork uow, IAuditTrail? audit = null)
    {
        _uow = uow;
        _audit = audit;
    }

    public async Task<ServiceResult<IReadOnlyList<OpeningPeriodDto>>> GetAsync(int clinicId)
    {
        if (!await _uow.Clinics.ExistsAsync(clinicId))
            return ServiceResult<IReadOnlyList<OpeningPeriodDto>>.Failure("Clinic not found.");

        var rows = await _uow.ClinicOpeningHours.Query()
            .Where(h => h.ClinicId == clinicId)
            .Select(h => new OpeningPeriodDto(h.DayOfWeek, h.OpensAt, h.ClosesAt))
            .ToListAsync();
        return ServiceResult<IReadOnlyList<OpeningPeriodDto>>.Success(OpeningHoursRules.Sorted(rows));
    }

    public async Task<ServiceResult> SaveAsync(int clinicId, IReadOnlyList<OpeningPeriodDto> periods, int actingUserId)
    {
        if (!await _uow.Clinics.ExistsAsync(clinicId))
            return ServiceResult.Failure("Clinic not found.");

        var errors = OpeningHoursRules.Validate(periods);
        if (errors.Count > 0)
            return ServiceResult.Failure(errors);

        var existing = (await _uow.ClinicOpeningHours.FindAsync(h => h.ClinicId == clinicId)).ToList();
        var before = Describe(existing.Select(h => new OpeningPeriodDto(h.DayOfWeek, h.OpensAt, h.ClosesAt)));
        var after = Describe(periods);
        if (before == after)
            return ServiceResult.Success("Opening hours unchanged.");

        _uow.ClinicOpeningHours.DeleteRange(existing);
        await _uow.ClinicOpeningHours.AddRangeAsync(periods.Select(p => new ClinicOpeningHours
        {
            ClinicId = clinicId,
            DayOfWeek = p.Day,
            OpensAt = p.Opens,
            ClosesAt = p.Closes,
        }));
        if (_audit is not null)
            await _audit.RecordAsync(AuditActions.ClinicHoursChanged, "Clinic", clinicId, new { before, after }, actingUserId);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Opening hours saved.");
    }

    /// <summary>"Saturday 10:00–14:00, 17:00–21:00; Sunday …" (for the audit log).</summary>
    private static string Describe(IEnumerable<OpeningPeriodDto> periods) =>
        string.Join("; ", OpeningHoursRules.Sorted(periods)
            .GroupBy(p => p.Day)
            .Select(g => $"{g.Key} {string.Join(", ", g.Select(p => p.Period.ToString()))}"));
}
