namespace SafyaClinic.Application.Scheduling;

/// <summary>P1c: a date that differs from the weekly pattern: a day off, or that date's own periods.</summary>
public sealed record DateChangeDto(DateTime Date, bool IsDayOff, IReadOnlyList<ScheduleTimeDto> Times);

/// <summary>P1c: an existing booking of the doctor (clinic time).</summary>
public sealed record BookingSlot(int ReservationId, int DoctorId, int ClinicId, DateTime Date, TimeSpan Start, int DurationMinutes)
{
    public TimePeriod Period => new(Start, Start + TimeSpan.FromMinutes(DurationMinutes));
}

/// <summary>P1c: everything that decides when a doctor can be booked in one clinic.</summary>
public sealed record CalendarInputs(
    IReadOnlyList<OpeningPeriodDto> ClinicHours, DoctorScheduleDto? Schedule, IReadOnlyList<DateChangeDto> DateChanges);

/// <summary>
/// P1c: when a doctor can be booked. One pure calculation used by the schedule screens now and by booking (P2).
/// <list type="bullet">
/// <item>No calendar: the clinic's opening hours, with a 15-minute step (decision S4).</item>
/// <item>A clinic that hasn't set any opening hours yet isn't restricted (P2 transition): its doctors without a
/// calendar can be booked at any time of the day.</item>
/// <item>Calendar not in use: not bookable in that clinic.</item>
/// <item>A date change replaces that day's weekly pattern; a day off means not bookable.</item>
/// <item>Open calendar: working periods minus breaks. Specific slots: the slots. Always clipped to the clinic's hours.</item>
/// <item>Free start times: every step from the start of each period where the whole visit fits, the doctor has no
/// other booking (in any clinic), and the time isn't in the past.</item>
/// </list>
/// </summary>
public static class Availability
{
    public const int FallbackStepMinutes = 15;

    /// <summary>The bookable day of a clinic that hasn't set its opening hours yet.</summary>
    public static readonly TimePeriod WholeDay = new(TimeSpan.Zero, TimeSpan.FromDays(1));

    /// <summary>The bookable periods on a date and the booking step.</summary>
    public static (IReadOnlyList<TimePeriod> Periods, int StepMinutes) For(DateTime date, CalendarInputs inputs)
    {
        var open = inputs.ClinicHours.Count == 0
            ? new List<TimePeriod> { WholeDay }
            : inputs.ClinicHours.Where(h => h.Day == date.DayOfWeek).Select(h => h.Period).OrderBy(p => p.Start).ToList();
        var schedule = inputs.Schedule;
        if (schedule is null)
            return (open, FallbackStepMinutes);
        if (!schedule.IsActive || open.Count == 0)
            return (Array.Empty<TimePeriod>(), schedule.StepMinutes);

        var change = inputs.DateChanges.FirstOrDefault(c => c.Date.Date == date.Date);
        if (change is { IsDayOff: true })
            return (Array.Empty<TimePeriod>(), schedule.StepMinutes);
        var times = change?.Times ?? schedule.Times.Where(t => t.Day == date.DayOfWeek).ToList();

        IEnumerable<TimePeriod> periods;
        if (schedule.Mode == Domain.Enums.ScheduleMode.Open)
        {
            var breaks = times.Where(t => t.Kind == Domain.Enums.ScheduleTimeKind.Break).Select(t => t.Period).ToList();
            periods = times.Where(t => t.Kind == Domain.Enums.ScheduleTimeKind.Working)
                .SelectMany(w => Subtract(w.Period, breaks));
        }
        else
        {
            periods = times.Where(t => t.Kind == Domain.Enums.ScheduleTimeKind.Slot).Select(t => t.Period);
        }

        var clipped = periods
            .SelectMany(p => open.Select(h => new TimePeriod(Max(p.Start, h.Start), Min(p.End, h.End))))
            .Where(p => p.End > p.Start)
            .OrderBy(p => p.Start)
            .ToList();
        return (clipped, schedule.StepMinutes);
    }

    /// <summary>Whether a visit lies wholly inside one bookable period (other bookings are not considered).</summary>
    public static bool Fits(DateTime date, TimePeriod visit, CalendarInputs inputs) =>
        For(date, inputs).Periods.Any(p => visit.Start >= p.Start && visit.End <= p.End);

    /// <summary>The free start times for a visit of this length on a date.</summary>
    public static IReadOnlyList<TimeSpan> FreeStarts(
        DateTime date, int visitMinutes, CalendarInputs inputs, IEnumerable<BookingSlot> doctorBookings, DateTime clinicNow)
    {
        if (visitMinutes <= 0 || date.Date < clinicNow.Date)
            return Array.Empty<TimeSpan>();

        var (periods, step) = For(date, inputs);
        var length = TimeSpan.FromMinutes(visitMinutes);
        var busy = doctorBookings.Where(b => b.Date.Date == date.Date).Select(b => b.Period).ToList();
        var earliest = date.Date == clinicNow.Date ? clinicNow.TimeOfDay : TimeSpan.Zero;

        var starts = new List<TimeSpan>();
        foreach (var period in periods)
        {
            for (var start = period.Start; start + length <= period.End; start += TimeSpan.FromMinutes(step))
            {
                var visit = new TimePeriod(start, start + length);
                if (start >= earliest && !busy.Any(b => b.Overlaps(visit)))
                    starts.Add(start);
            }
        }
        return starts.Distinct().OrderBy(s => s).ToList();
    }

    /// <summary>
    /// Future bookings that fit the calendar before a change but not after it. They must be moved or cancelled
    /// before the change can be saved. Bookings that already didn't fit are not reported.
    /// </summary>
    public static IReadOnlyList<BookingSlot> Stranded(
        CalendarInputs before, CalendarInputs after, IEnumerable<BookingSlot> futureBookings) =>
        futureBookings
            .Where(b => Fits(b.Date, b.Period, before) && !Fits(b.Date, b.Period, after))
            .OrderBy(b => b.Date).ThenBy(b => b.Start)
            .ToList();

    /// <summary>"#12 Sat 14 Nov 2026 10:00" for messages (no patient details).</summary>
    public static string Describe(BookingSlot b) =>
        $"#{b.ReservationId} {b.Date.ToString("ddd dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)} {b.Start.ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture)}";

    private static IEnumerable<TimePeriod> Subtract(TimePeriod period, IReadOnlyList<TimePeriod> gaps)
    {
        var pieces = new List<TimePeriod> { period };
        foreach (var gap in gaps)
            pieces = pieces.SelectMany(p => !p.Overlaps(gap)
                    ? new[] { p }
                    : new[] { new TimePeriod(p.Start, gap.Start), new TimePeriod(gap.End, p.End) })
                .Where(p => p.End > p.Start)
                .ToList();
        return pieces;
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
}
