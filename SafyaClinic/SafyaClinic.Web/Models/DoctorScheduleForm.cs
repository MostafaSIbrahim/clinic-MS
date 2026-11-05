using SafyaClinic.Application.Scheduling;
using SafyaClinic.Domain.Enums;

namespace SafyaClinic.Web.Models;

/// <summary>
/// P1: the calendar form for one doctor in one clinic: mode, step, in use, and per weekday (Saturday first)
/// working periods and breaks (open calendar) or slots (specific slots). Only the chosen mode's rows are saved.
/// </summary>
public class DoctorScheduleForm
{
    public int DoctorId { get; set; }
    public int ClinicId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string ClinicName { get; set; } = string.Empty;
    public ScheduleMode Mode { get; set; } = ScheduleMode.Open;
    public int StepMinutes { get; set; } = 15;
    public bool IsActive { get; set; } = true;
    public List<ScheduleDayInput> Days { get; set; } = new();

    public static DoctorScheduleForm From(DoctorScheduleView view)
    {
        // A new calendar starts with the clinic's hours as working periods: add breaks or trim from there.
        var source = view.Exists
            ? view.Schedule.Times
            : view.ClinicHours.Select(h => new ScheduleTimeDto(h.Day, ScheduleTimeKind.Working, h.Opens, h.Closes)).ToList();
        var times = source.ToLookup(t => (t.Day, t.Kind));
        List<PeriodInput> Rows(DayOfWeek day, ScheduleTimeKind kind) =>
            times[(day, kind)].OrderBy(t => t.Start)
                .Select(t => new PeriodInput { Opens = t.Start, Closes = t.End })
                .Concat(Enumerable.Range(0, DoctorScheduleRules.MaxPerDay(kind)).Select(_ => new PeriodInput()))
                .Take(DoctorScheduleRules.MaxPerDay(kind))
                .ToList();

        var form = new DoctorScheduleForm
        {
            DoctorId = view.Schedule.DoctorId,
            ClinicId = view.Schedule.ClinicId,
            Mode = view.Schedule.Mode,
            StepMinutes = view.Schedule.StepMinutes,
            IsActive = view.Schedule.IsActive,
            Days = ClinicWeek.Days.Select(day => new ScheduleDayInput
            {
                Day = day,
                Working = Rows(day, ScheduleTimeKind.Working),
                Breaks = Rows(day, ScheduleTimeKind.Break),
                Slots = Rows(day, ScheduleTimeKind.Slot),
            }).ToList(),
        };
        form.ShowDetails(view);
        return form;
    }

    /// <summary>Fills what is only displayed (names, clinic hours), e.g. after a failed save.</summary>
    public void ShowDetails(DoctorScheduleView view)
    {
        DoctorName = view.DoctorName;
        ClinicName = view.ClinicName;
        var hours = view.ClinicHours.ToLookup(h => h.Day);
        foreach (var day in Days)
            day.ClinicHours = hours[day.Day].Select(h => h.Period.ToString()).ToList();
    }

    /// <summary>The calendar to save: only the chosen mode's rows. Empty rows are ignored; half-filled rows are errors.</summary>
    public DoctorScheduleDto ToSchedule(out IReadOnlyList<string> errors)
    {
        var found = new List<string>();
        var times = new List<ScheduleTimeDto>();
        void Read(DayOfWeek day, ScheduleTimeKind kind, List<PeriodInput> rows)
        {
            for (var k = 0; k < rows.Count; k++)
            {
                var r = rows[k];
                if (r.Opens is null && r.Closes is null) continue;
                if (r.Opens is null || r.Closes is null)
                {
                    found.Add($"{day}, {kind.ToString().ToLowerInvariant()} {k + 1}: enter both the start and the end time.");
                    continue;
                }
                times.Add(new ScheduleTimeDto(day, kind, r.Opens.Value, r.Closes.Value));
            }
        }

        foreach (var day in Days)
        {
            if (Mode == ScheduleMode.Open)
            {
                Read(day.Day, ScheduleTimeKind.Working, day.Working);
                Read(day.Day, ScheduleTimeKind.Break, day.Breaks);
            }
            else
            {
                Read(day.Day, ScheduleTimeKind.Slot, day.Slots);
            }
        }
        errors = found;
        return new DoctorScheduleDto(DoctorId, ClinicId, Mode, StepMinutes, IsActive, times);
    }
}

public class ScheduleDayInput
{
    public DayOfWeek Day { get; set; }
    public List<string> ClinicHours { get; set; } = new();
    public List<PeriodInput> Working { get; set; } = new();
    public List<PeriodInput> Breaks { get; set; } = new();
    public List<PeriodInput> Slots { get; set; } = new();
}

/// <summary>P1c: one date change: a day off, or that date's own periods for the calendar's mode.</summary>
public class DateChangeForm
{
    public DateTime? Date { get; set; }
    public bool IsDayOff { get; set; } = true;
    public List<PeriodInput> Working { get; set; } = Rows(ScheduleTimeKind.Working);
    public List<PeriodInput> Breaks { get; set; } = Rows(ScheduleTimeKind.Break);
    public List<PeriodInput> Slots { get; set; } = Rows(ScheduleTimeKind.Slot);

    private static List<PeriodInput> Rows(ScheduleTimeKind kind) =>
        Enumerable.Range(0, DoctorScheduleRules.MaxPerDay(kind)).Select(_ => new PeriodInput()).ToList();

    public DateChangeDto? ToChange(ScheduleMode mode, out IReadOnlyList<string> errors)
    {
        var found = new List<string>();
        if (Date is null)
        {
            errors = new[] { "Choose a date." };
            return null;
        }
        var day = Date.Value.DayOfWeek;
        var times = new List<ScheduleTimeDto>();
        void Read(ScheduleTimeKind kind, List<PeriodInput> rows)
        {
            for (var k = 0; k < rows.Count; k++)
            {
                var r = rows[k];
                if (r.Opens is null && r.Closes is null) continue;
                if (r.Opens is null || r.Closes is null)
                {
                    found.Add($"{kind.ToString().ToLowerInvariant()} {k + 1}: enter both the start and the end time.");
                    continue;
                }
                times.Add(new ScheduleTimeDto(day, kind, r.Opens.Value, r.Closes.Value));
            }
        }
        if (!IsDayOff)
        {
            if (mode == ScheduleMode.Open)
            {
                Read(ScheduleTimeKind.Working, Working);
                Read(ScheduleTimeKind.Break, Breaks);
            }
            else
            {
                Read(ScheduleTimeKind.Slot, Slots);
            }
        }
        errors = found;
        return new DateChangeDto(Date.Value.Date, IsDayOff, times);
    }
}
