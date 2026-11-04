using SafyaClinic.Application.Scheduling;

namespace SafyaClinic.Web.Models;

/// <summary>P1: the opening-hours form: every weekday (Saturday first) with up to three periods.</summary>
public class ClinicHoursForm
{
    public int ClinicId { get; set; }
    public string ClinicName { get; set; } = string.Empty;
    public List<DayHoursInput> Days { get; set; } = new();

    public static ClinicHoursForm From(int clinicId, string clinicName, IEnumerable<OpeningPeriodDto> periods)
    {
        var byDay = periods.ToLookup(p => p.Day);
        return new ClinicHoursForm
        {
            ClinicId = clinicId,
            ClinicName = clinicName,
            Days = ClinicWeek.Days.Select(day => new DayHoursInput
            {
                Day = day,
                Periods = byDay[day].OrderBy(p => p.Opens)
                    .Select(p => new PeriodInput { Opens = p.Opens, Closes = p.Closes })
                    .Concat(Enumerable.Range(0, OpeningHoursRules.MaxPeriodsPerDay).Select(_ => new PeriodInput()))
                    .Take(OpeningHoursRules.MaxPeriodsPerDay)
                    .ToList(),
            }).ToList(),
        };
    }

    /// <summary>The filled-in periods. A row with neither time is ignored; a row with only one is an error.</summary>
    public IReadOnlyList<OpeningPeriodDto> ToPeriods(out IReadOnlyList<string> errors)
    {
        var found = new List<string>();
        var periods = new List<OpeningPeriodDto>();
        foreach (var day in Days)
        {
            for (var k = 0; k < day.Periods.Count; k++)
            {
                var p = day.Periods[k];
                if (p.Opens is null && p.Closes is null) continue;
                if (p.Opens is null || p.Closes is null)
                {
                    found.Add($"{day.Day}, period {k + 1}: enter both the opening and the closing time.");
                    continue;
                }
                periods.Add(new OpeningPeriodDto(day.Day, p.Opens.Value, p.Closes.Value));
            }
        }
        errors = found;
        return periods;
    }
}

public class DayHoursInput
{
    public DayOfWeek Day { get; set; }
    public List<PeriodInput> Periods { get; set; } = new();
}

public class PeriodInput
{
    public TimeSpan? Opens { get; set; }
    public TimeSpan? Closes { get; set; }
}
