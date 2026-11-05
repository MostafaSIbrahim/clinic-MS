using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Common;
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
    private readonly IClinicClock _clock;

    public DoctorScheduleService(IUnitOfWork uow, IAuditTrail? audit = null, IClinicClock? clock = null)
    {
        _uow = uow;
        _audit = audit;
        _clock = clock ?? new ClinicClock(ClinicClock.FindZone(ClinicClock.DefaultTimeZoneId));
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

        var hours = await CalendarData.ClinicHoursAsync(_uow, clinicId);
        var (schedule, changes) = await CalendarData.ScheduleAsync(_uow, doctorId, clinicId, _clock.Today);

        var dto = schedule
            ?? new DoctorScheduleDto(doctorId, clinicId, ScheduleMode.Open, DefaultStepMinutes, true, Array.Empty<ScheduleTimeDto>());
        return ServiceResult<DoctorScheduleView>.Success(
            new DoctorScheduleView(doctor, clinic, schedule is not null, dto, hours) { DateChanges = changes });
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

        // P1c: never strand a future booking.
        var stranded = await StrandedAsync(dto.DoctorId, dto.ClinicId, view.Data.Exists ? view.Data.Schedule : null, dto,
            view.Data.DateChanges, view.Data.DateChanges, view.Data.ClinicHours);
        if (stranded.Count > 0)
            return ServiceResult.Failure(CalendarData.StrandedMessage(stranded));

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

    // ── Date changes (P1c) ──────────────────────────────────────

    public async Task<ServiceResult> SetDateChangeAsync(int doctorId, int clinicId, DateChangeDto change, int actingUserId)
    {
        var view = await GetAsync(doctorId, clinicId);
        if (!view.IsSuccess)
            return ServiceResult.Failure(view.Errors);
        if (!view.Data!.Exists)
            return ServiceResult.Failure("Save the weekly calendar first, then add date changes.");

        var date = change.Date.Date;
        if (date < _clock.Today)
            return ServiceResult.Failure("Choose today or a later date.");

        var schedule = view.Data.Schedule;
        var keep = schedule.Mode == ScheduleMode.Open
            ? new[] { ScheduleTimeKind.Working, ScheduleTimeKind.Break }
            : new[] { ScheduleTimeKind.Slot };
        IReadOnlyList<ScheduleTimeDto> times = change.IsDayOff
            ? Array.Empty<ScheduleTimeDto>()
            : change.Times.Where(t => keep.Contains(t.Kind)).Select(t => t with { Day = date.DayOfWeek }).ToList();
        if (!change.IsDayOff && times.Count == 0)
            return ServiceResult.Failure(schedule.Mode == ScheduleMode.Open
                ? "Enter that date's working periods, or choose a day off."
                : "Enter that date's slots, or choose a day off.");

        if (!change.IsDayOff)
        {
            // The date's own periods follow the same rules as a weekday, against the doctor's other clinics on that date.
            var others = await OtherClinicsOnAsync(doctorId, clinicId, date);
            var errors = DoctorScheduleRules.Validate(schedule with { IsActive = true, Times = times }, view.Data.ClinicHours, others);
            if (errors.Count > 0)
                return ServiceResult.Failure(errors);
        }

        var newChange = new DateChangeDto(date, change.IsDayOff, times);
        var after = view.Data.DateChanges.Where(c => c.Date != date).Append(newChange).OrderBy(c => c.Date).ToList();
        var stranded = await StrandedAsync(doctorId, clinicId, schedule, schedule, view.Data.DateChanges, after, view.Data.ClinicHours);
        if (stranded.Count > 0)
            return ServiceResult.Failure(CalendarData.StrandedMessage(stranded));

        var entity = await _uow.DoctorSchedules.Query(asNoTracking: false)
            .Include(s => s.Changes)
            .FirstAsync(s => s.DoctorId == doctorId && s.ClinicId == clinicId);
        _uow.DoctorScheduleChanges.DeleteRange(entity.Changes.Where(c => c.Date.Date == date).ToList());
        if (change.IsDayOff)
            entity.Changes.Add(new DoctorScheduleChange { Date = date, IsDayOff = true });
        else
            foreach (var t in times)
                entity.Changes.Add(new DoctorScheduleChange { Date = date, Kind = t.Kind, StartTime = t.Start, EndTime = t.End });

        if (_audit is not null)
            await _audit.RecordAsync(AuditActions.ScheduleDateChanged, "DoctorSchedule", entity.Id,
                new { doctorId, clinicId, date = date.ToString("yyyy-MM-dd"), change = Describe(newChange) }, actingUserId);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success(change.IsDayOff ? "Day off saved." : "Date change saved.");
    }

    public async Task<ServiceResult> RemoveDateChangeAsync(int doctorId, int clinicId, DateTime date, int actingUserId)
    {
        var view = await GetAsync(doctorId, clinicId);
        if (!view.IsSuccess)
            return ServiceResult.Failure(view.Errors);

        date = date.Date;
        var existing = view.Data!.DateChanges.FirstOrDefault(c => c.Date == date);
        if (existing is null)
            return ServiceResult.Success("Nothing to remove.");

        var after = view.Data.DateChanges.Where(c => c.Date != date).ToList();
        var schedule = view.Data.Schedule;
        var stranded = await StrandedAsync(doctorId, clinicId, schedule, schedule, view.Data.DateChanges, after, view.Data.ClinicHours);
        if (stranded.Count > 0)
            return ServiceResult.Failure(CalendarData.StrandedMessage(stranded));

        var rows = await _uow.DoctorScheduleChanges.Query(asNoTracking: false)
            .Where(c => c.Schedule.DoctorId == doctorId && c.Schedule.ClinicId == clinicId && c.Date == date)
            .ToListAsync();
        _uow.DoctorScheduleChanges.DeleteRange(rows);
        if (_audit is not null)
            await _audit.RecordAsync(AuditActions.ScheduleDateChanged, "DoctorSchedule", rows.FirstOrDefault()?.ScheduleId,
                new { doctorId, clinicId, date = date.ToString("yyyy-MM-dd"), change = "removed" }, actingUserId);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Date change removed; the weekly calendar applies again.");
    }

    // ── Free times (P1c preview; P2 booking) ────────────────────

    public async Task<ServiceResult<IReadOnlyList<TimeSpan>>> FreeStartsAsync(int doctorId, int clinicId, DateTime date, int visitMinutes)
    {
        var inputs = await CalendarData.InputsAsync(_uow, doctorId, clinicId, date);
        var bookings = await CalendarData.BookingsAsync(_uow, date, doctorId, clinicId: null, toDate: date);
        return ServiceResult<IReadOnlyList<TimeSpan>>.Success(
            Availability.FreeStarts(date, visitMinutes, inputs, bookings, _clock.Now));
    }

    // ── helpers ─────────────────────────────────────────────────

    private async Task<IReadOnlyList<BookingSlot>> StrandedAsync(
        int doctorId, int clinicId, DoctorScheduleDto? scheduleBefore, DoctorScheduleDto scheduleAfter,
        IReadOnlyList<DateChangeDto> changesBefore, IReadOnlyList<DateChangeDto> changesAfter, IReadOnlyList<OpeningPeriodDto> hours)
    {
        var bookings = CalendarData.Upcoming(await CalendarData.BookingsAsync(_uow, _clock.Today, doctorId, clinicId), _clock.Now);
        return Availability.Stranded(
            new CalendarInputs(hours, scheduleBefore, changesBefore),
            new CalendarInputs(hours, scheduleAfter, changesAfter),
            bookings);
    }

    /// <summary>The doctor's bookable periods in their other clinics on a date (for the overlap check).</summary>
    private async Task<IReadOnlyList<OtherClinicTime>> OtherClinicsOnAsync(int doctorId, int clinicId, DateTime date)
    {
        var others = await _uow.DoctorSchedules.Query()
            .Where(s => s.DoctorId == doctorId && s.ClinicId != clinicId && s.IsActive)
            .Select(s => new { s.ClinicId, ClinicName = s.Clinic.Name })
            .ToListAsync();
        var result = new List<OtherClinicTime>();
        foreach (var other in others)
        {
            var inputs = await CalendarData.InputsAsync(_uow, doctorId, other.ClinicId, date);
            result.AddRange(Availability.For(date, inputs).Periods
                .Select(p => new OtherClinicTime(other.ClinicName, new ScheduleTimeDto(date.DayOfWeek, ScheduleTimeKind.Working, p.Start, p.End))));
        }
        return result;
    }

    private static string Describe(DateChangeDto c) =>
        c.IsDayOff ? "day off" : string.Join(", ", c.Times.Select(t => $"{t.Kind.ToString().ToLowerInvariant()} {t.Period}"));

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
