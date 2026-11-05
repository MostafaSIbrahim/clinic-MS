using SafyaClinic.Domain.Enums;

namespace SafyaClinic.Application.Scheduling;

/// <summary>P1: one period of a doctor's weekly pattern.</summary>
public sealed record ScheduleTimeDto(DayOfWeek Day, ScheduleTimeKind Kind, TimeSpan Start, TimeSpan End)
{
    public TimePeriod Period => new(Start, End);
}

/// <summary>P1: a doctor's calendar in one clinic.</summary>
public sealed record DoctorScheduleDto(
    int DoctorId, int ClinicId, ScheduleMode Mode, int StepMinutes, bool IsActive, IReadOnlyList<ScheduleTimeDto> Times)
{
    /// <summary>The periods in which the doctor can be booked: working periods (open) or slots (specific slots).</summary>
    public IEnumerable<ScheduleTimeDto> Availability =>
        Times.Where(t => t.Kind == (Mode == ScheduleMode.Open ? ScheduleTimeKind.Working : ScheduleTimeKind.Slot));
}

/// <summary>P1: a doctor's availability in another clinic, for the overlap check.</summary>
public sealed record OtherClinicTime(string ClinicName, ScheduleTimeDto Time);

/// <summary>P1: what the schedule screen needs.</summary>
public sealed record DoctorScheduleView(
    string DoctorName, string ClinicName, bool Exists, DoctorScheduleDto Schedule, IReadOnlyList<OpeningPeriodDto> ClinicHours)
{
    /// <summary>P1c: date changes from today on.</summary>
    public IReadOnlyList<DateChangeDto> DateChanges { get; init; } = Array.Empty<DateChangeDto>();
}

/// <summary>P1: one row of the schedules list (a clinician in a clinic).</summary>
public sealed record ScheduleSummaryDto(
    int DoctorId, string DoctorName, int ClinicId, string ClinicName, bool IsSet, ScheduleMode Mode, int StepMinutes, bool IsActive);

/// <summary>
/// P1: rules for a doctor's weekly pattern in one clinic.
/// <list type="bullet">
/// <item>Open: working periods (up to 3 a day) and breaks (up to 2) inside them. Specific slots: slots (up to 4 a day), each at least one step long.</item>
/// <item>Working periods and slots lie inside the clinic's opening hours for that day.</item>
/// <item>An active schedule needs at least one working period or slot, and never overlaps the doctor's
/// active schedules in other clinics (touching is fine).</item>
/// </list>
/// </summary>
public static class DoctorScheduleRules
{
    public static readonly IReadOnlyList<int> Steps = new[] { 5, 10, 15, 20, 30, 60 };
    public const int MaxWorkingPerDay = 3;
    public const int MaxBreaksPerDay = 2;
    public const int MaxSlotsPerDay = 4;

    public static int MaxPerDay(ScheduleTimeKind kind) => kind switch
    {
        ScheduleTimeKind.Working => MaxWorkingPerDay,
        ScheduleTimeKind.Break => MaxBreaksPerDay,
        _ => MaxSlotsPerDay,
    };

    public static IReadOnlyList<string> Validate(
        DoctorScheduleDto schedule, IEnumerable<OpeningPeriodDto> clinicHours, IEnumerable<OtherClinicTime> otherClinics)
    {
        var errors = new List<string>();
        if (!Steps.Contains(schedule.StepMinutes))
            errors.Add($"Choose a step of {string.Join(", ", Steps)} minutes.");

        var allowed = schedule.Mode == ScheduleMode.Open
            ? new[] { ScheduleTimeKind.Working, ScheduleTimeKind.Break }
            : new[] { ScheduleTimeKind.Slot };
        foreach (var kind in schedule.Times.Select(t => t.Kind).Distinct().Where(k => !allowed.Contains(k)))
            errors.Add($"{Name(kind)}s don't belong in a {Name(schedule.Mode)} calendar.");

        var hours = clinicHours.ToLookup(h => h.Day);
        var step = TimeSpan.FromMinutes(schedule.StepMinutes);

        foreach (var day in schedule.Times.Where(t => allowed.Contains(t.Kind)).GroupBy(t => t.Day).OrderBy(g => ClinicWeek.Position(g.Key)))
        {
            foreach (var kind in day.GroupBy(t => t.Kind))
            {
                var list = kind.OrderBy(t => t.Start).ToList();
                if (list.Count > MaxPerDay(kind.Key))
                    errors.Add($"{day.Key}: at most {MaxPerDay(kind.Key)} {Name(kind.Key).ToLowerInvariant()}s a day.");
                for (var k = 1; k < list.Count; k++)
                    if (list[k].Period.Overlaps(list[k - 1].Period))
                        errors.Add($"{day.Key}: {list[k - 1].Period} and {list[k].Period} overlap.");
            }

            var working = day.Where(t => t.Kind == ScheduleTimeKind.Working).ToList();
            foreach (var t in day)
            {
                if (t.Start < TimeSpan.Zero || t.End >= TimeSpan.FromDays(1) || t.End <= t.Start)
                {
                    errors.Add($"{day.Key}: {t.Period} must end after it starts, within the day.");
                    continue;
                }
                switch (t.Kind)
                {
                    case ScheduleTimeKind.Break when working.Count == 0:
                        errors.Add($"{day.Key}: the break {t.Period} must be inside a working period, and none is set that day. Add the doctor's working period (for example the clinic's hours) first.");
                        break;
                    case ScheduleTimeKind.Break when !working.Any(w => Inside(t.Period, w.Period)):
                        errors.Add($"{day.Key}: the break {t.Period} must be inside a working period ({string.Join(", ", working.OrderBy(w => w.Start).Select(w => w.Period.ToString()))}).");
                        break;
                    case ScheduleTimeKind.Working or ScheduleTimeKind.Slot when !hours[day.Key].Any():
                        errors.Add($"{day.Key}: the clinic is closed that day.");
                        break;
                    case ScheduleTimeKind.Working or ScheduleTimeKind.Slot when !hours[day.Key].Any(h => Inside(t.Period, h.Period)):
                        errors.Add($"{day.Key}: {t.Period} is outside the clinic's opening hours.");
                        break;
                    case ScheduleTimeKind.Slot when t.End - t.Start < step:
                        errors.Add($"{day.Key}: the slot {t.Period} is shorter than the {schedule.StepMinutes}-minute step.");
                        break;
                }
            }
        }

        if (schedule.IsActive)
        {
            var availability = schedule.Availability.ToList();
            if (availability.Count == 0)
                errors.Add(schedule.Mode == ScheduleMode.Open
                    ? "Add at least one working period, or mark the calendar as not in use."
                    : "Add at least one slot, or mark the calendar as not in use.");

            var others = otherClinics.ToList();
            foreach (var mine in availability.OrderBy(t => ClinicWeek.Position(t.Day)).ThenBy(t => t.Start))
                foreach (var other in others.Where(o => o.Time.Day == mine.Day && o.Time.Period.Overlaps(mine.Period)))
                    errors.Add($"{mine.Day}: {mine.Period} overlaps the doctor's hours at {other.ClinicName} ({other.Time.Period}).");
        }

        return errors.Distinct().ToList();
    }

    private static bool Inside(TimePeriod inner, TimePeriod outer) => inner.Start >= outer.Start && inner.End <= outer.End;

    private static string Name(ScheduleTimeKind kind) => kind switch
    {
        ScheduleTimeKind.Working => "Working period",
        ScheduleTimeKind.Break => "Break",
        _ => "Slot",
    };

    private static string Name(ScheduleMode mode) => mode == ScheduleMode.Open ? "open" : "specific-slots";
}
