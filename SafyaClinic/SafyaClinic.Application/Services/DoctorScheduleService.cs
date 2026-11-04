using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Common.Auditing;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.Scheduling;
using SafyaClinic.Domain.Entities.Scheduling;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Interfaces.Repositories;

namespace SafyaClinic.Application.Services;

/// <summary>P1: reads and saves doctors' calendars; every change is audited.</summary>
public class DoctorScheduleService : IDoctorScheduleService
{
    public const int DefaultStepMinutes = 15;

    private readonly IUnitOfWork _uow;
    private readonly IAuditTrail? _audit;

    public DoctorScheduleService(IUnitOfWork uow, IAuditTrail? audit = null)
    {
        _uow = uow;
        _audit = audit;
    }

    public async Task<ServiceResult<IReadOnlyList<ScheduleSummaryDto>>> ListAsync(int? doctorId = null)
    {
        var doctors = await _uow.Users.Query()
            .Where(u => u.IsActive && u.ClinicianType != ClinicianType.None && (doctorId == null || u.Id == doctorId))
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync();
        var clinics = await _uow.Clinics.Query()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();
        var schedules = await _uow.DoctorSchedules.Query()
            .Where(s => doctorId == null || s.DoctorId == doctorId)
            .ToDictionaryAsync(s => (s.DoctorId, s.ClinicId));

        var rows = doctors
            .SelectMany(d => clinics.Select(c =>
            {
                var found = schedules.TryGetValue((d.Id, c.Id), out var s);
                return new ScheduleSummaryDto(d.Id, d.FullName, c.Id, c.Name, found,
                    s?.Mode ?? ScheduleMode.Open, s?.StepMinutes ?? DefaultStepMinutes, s?.IsActive ?? true);
            }))
            .ToList();
        return ServiceResult<IReadOnlyList<ScheduleSummaryDto>>.Success(rows);
    }

    public async Task<ServiceResult<DoctorScheduleView>> GetAsync(int doctorId, int clinicId)
    {
        var doctor = await _uow.Users.Query()
            .Where(u => u.Id == doctorId && u.IsActive && u.ClinicianType != ClinicianType.None)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync();
        if (doctor is null)
            return ServiceResult<DoctorScheduleView>.Failure("Only active clinicians have a calendar.");

        var clinic = await _uow.Clinics.Query().Where(c => c.Id == clinicId && c.IsActive).Select(c => c.Name).FirstOrDefaultAsync();
        if (clinic is null)
            return ServiceResult<DoctorScheduleView>.Failure("Clinic not found.");

        var hours = await ClinicHoursAsync(clinicId);
        var schedule = await _uow.DoctorSchedules.Query()
            .Include(s => s.Times)
            .FirstOrDefaultAsync(s => s.DoctorId == doctorId && s.ClinicId == clinicId);

        var dto = schedule is null
            ? new DoctorScheduleDto(doctorId, clinicId, ScheduleMode.Open, DefaultStepMinutes, true, Array.Empty<ScheduleTimeDto>())
            : ToDto(schedule);
        return ServiceResult<DoctorScheduleView>.Success(new DoctorScheduleView(doctor, clinic, schedule is not null, dto, hours));
    }

    public async Task<ServiceResult> SaveAsync(DoctorScheduleDto dto, int actingUserId)
    {
        var view = await GetAsync(dto.DoctorId, dto.ClinicId);
        if (!view.IsSuccess)
            return ServiceResult.Failure(view.Errors);

        // Only the periods that belong to the chosen mode are kept.
        var keep = dto.Mode == ScheduleMode.Open
            ? new[] { ScheduleTimeKind.Working, ScheduleTimeKind.Break }
            : new[] { ScheduleTimeKind.Slot };
        dto = dto with { Times = dto.Times.Where(t => keep.Contains(t.Kind)).ToList() };

        var others = await _uow.DoctorSchedules.Query()
            .Where(s => s.DoctorId == dto.DoctorId && s.ClinicId != dto.ClinicId && s.IsActive)
            .Select(s => new { ClinicName = s.Clinic.Name, s.Mode, s.Times })
            .ToListAsync();
        var otherTimes = others.SelectMany(o => o.Times
                .Where(t => t.Kind == (o.Mode == ScheduleMode.Open ? ScheduleTimeKind.Working : ScheduleTimeKind.Slot))
                .Select(t => new OtherClinicTime(o.ClinicName, new ScheduleTimeDto(t.DayOfWeek, t.Kind, t.StartTime, t.EndTime))))
            .ToList();

        var errors = DoctorScheduleRules.Validate(dto, view.Data!.ClinicHours, otherTimes);
        if (errors.Count > 0)
            return ServiceResult.Failure(errors);

        var before = view.Data.Exists ? Describe(view.Data.Schedule) : "not set";
        var after = Describe(dto);
        if (before == after)
            return ServiceResult.Success("Calendar unchanged.");

        var entity = await _uow.DoctorSchedules.Query(asNoTracking: false)
            .Include(s => s.Times)
            .FirstOrDefaultAsync(s => s.DoctorId == dto.DoctorId && s.ClinicId == dto.ClinicId);
        if (entity is null)
        {
            entity = new DoctorSchedule { DoctorId = dto.DoctorId, ClinicId = dto.ClinicId };
            await _uow.DoctorSchedules.AddAsync(entity);
        }
        else
        {
            _uow.DoctorScheduleTimes.DeleteRange(entity.Times.ToList());
            entity.Times.Clear();
        }

        entity.Mode = dto.Mode;
        entity.StepMinutes = dto.StepMinutes;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.UpdatedBy = actingUserId;
        foreach (var t in dto.Times)
            entity.Times.Add(new DoctorScheduleTime { DayOfWeek = t.Day, Kind = t.Kind, StartTime = t.Start, EndTime = t.End });

        if (_audit is not null)
            await _audit.RecordAsync(AuditActions.ScheduleChanged, "DoctorSchedule", entity.Id == 0 ? null : entity.Id,
                new { doctorId = dto.DoctorId, clinicId = dto.ClinicId, before, after }, actingUserId);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Calendar saved.");
    }

    private async Task<IReadOnlyList<OpeningPeriodDto>> ClinicHoursAsync(int clinicId)
    {
        var rows = await _uow.ClinicOpeningHours.Query()
            .Where(h => h.ClinicId == clinicId)
            .Select(h => new OpeningPeriodDto(h.DayOfWeek, h.OpensAt, h.ClosesAt))
            .ToListAsync();
        return OpeningHoursRules.Sorted(rows);
    }

    private static DoctorScheduleDto ToDto(DoctorSchedule s) =>
        new(s.DoctorId, s.ClinicId, s.Mode, s.StepMinutes, s.IsActive,
            s.Times.Select(t => new ScheduleTimeDto(t.DayOfWeek, t.Kind, t.StartTime, t.EndTime))
                .OrderBy(t => ClinicWeek.Position(t.Day)).ThenBy(t => t.Kind).ThenBy(t => t.Start)
                .ToList());

    /// <summary>"Open, 15 min, in use; Saturday working 10:00–14:00, break 12:00–12:30; …" (for the audit log).</summary>
    private static string Describe(DoctorScheduleDto s)
    {
        var days = s.Times
            .OrderBy(t => ClinicWeek.Position(t.Day)).ThenBy(t => t.Kind).ThenBy(t => t.Start)
            .GroupBy(t => t.Day)
            .Select(g => $"{g.Key} " + string.Join(", ", g.Select(t => $"{t.Kind.ToString().ToLowerInvariant()} {t.Period}")));
        return $"{s.Mode}, {s.StepMinutes} min, {(s.IsActive ? "in use" : "not in use")}; " + string.Join("; ", days);
    }
}
