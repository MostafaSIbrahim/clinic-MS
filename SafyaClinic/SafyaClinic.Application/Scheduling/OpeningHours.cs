using System.Globalization;

namespace SafyaClinic.Application.Scheduling;

/// <summary>P1: a period of a day in clinic time, for example 10:00–14:00. Start is included, end is not.</summary>
public readonly record struct TimePeriod(TimeSpan Start, TimeSpan End)
{
    public bool Overlaps(TimePeriod other) => Start < other.End && other.Start < End;

    public override string ToString() =>
        $"{Start.ToString(@"hh\:mm", CultureInfo.InvariantCulture)}–{End.ToString(@"hh\:mm", CultureInfo.InvariantCulture)}";
}

/// <summary>P1: one opening period of a clinic on a weekday.</summary>
public sealed record OpeningPeriodDto(DayOfWeek Day, TimeSpan Opens, TimeSpan Closes)
{
    public TimePeriod Period => new(Opens, Closes);
}

/// <summary>P1: the clinic week, as the clinic reads it (Saturday first).</summary>
public static class ClinicWeek
{
    public static IReadOnlyList<DayOfWeek> Days { get; } = new[]
    {
        DayOfWeek.Saturday, DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday,
        DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
    };

    /// <summary>Saturday = 0 … Friday = 6, for sorting.</summary>
    public static int Position(DayOfWeek day) => ((int)day + 1) % 7;
}

/// <summary>
/// P1: rules for a clinic's weekly opening hours. Each day has up to <see cref="MaxPeriodsPerDay"/>
/// periods; a period ends after it starts on the same day (no overnight periods); periods on the same
/// day may touch (14:00–17:00 after 10:00–14:00) but not overlap. A day without periods is closed.
/// </summary>
public static class OpeningHoursRules
{
    public const int MaxPeriodsPerDay = 3;
    public static readonly TimeSpan MinimumLength = TimeSpan.FromMinutes(15);

    public static IReadOnlyList<string> Validate(IEnumerable<OpeningPeriodDto> periods)
    {
        var errors = new List<string>();
        foreach (var day in periods.GroupBy(p => p.Day).OrderBy(g => ClinicWeek.Position(g.Key)))
        {
            var list = day.OrderBy(p => p.Opens).ToList();
            if (list.Count > MaxPeriodsPerDay)
                errors.Add($"{day.Key}: at most {MaxPeriodsPerDay} periods a day.");

            foreach (var p in list)
            {
                if (p.Opens < TimeSpan.Zero || p.Closes >= TimeSpan.FromDays(1))
                    errors.Add($"{day.Key}: times must be within the day.");
                else if (p.Closes <= p.Opens)
                    errors.Add($"{day.Key}: {p.Period} must close after it opens.");
                else if (p.Closes - p.Opens < MinimumLength)
                    errors.Add($"{day.Key}: {p.Period} is shorter than {MinimumLength.TotalMinutes:0} minutes.");
            }

            for (var k = 1; k < list.Count; k++)
                if (list[k].Period.Overlaps(list[k - 1].Period))
                    errors.Add($"{day.Key}: {list[k - 1].Period} and {list[k].Period} overlap.");
        }
        return errors;
    }

    /// <summary>Days in clinic order, periods by start time.</summary>
    public static IReadOnlyList<OpeningPeriodDto> Sorted(IEnumerable<OpeningPeriodDto> periods) =>
        periods.OrderBy(p => ClinicWeek.Position(p.Day)).ThenBy(p => p.Opens).ToList();
}
