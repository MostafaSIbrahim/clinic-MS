using System.Globalization;

namespace SafyaClinic.Application.Scheduling;

/// <summary>
/// P2: whether a reservation may take a time (D7). Checked by the server, inside the doctor's booking lock,
/// whenever a reservation is created or its doctor, clinic, date, time or length changes:
/// <list type="bullet">
/// <item>not in the past (clinic time);</item>
/// <item>wholly inside the doctor's bookable hours in that clinic on that date (<see cref="Availability"/>);</item>
/// <item>no overlap with the doctor's other bookings in any clinic (no double booking).</item>
/// </list>
/// </summary>
public static class BookingRules
{
    public const int MinVisitMinutes = 5;
    public const int MaxVisitMinutes = 480;

    /// <summary>Why the time can't be booked, or null when it can.</summary>
    public static string? Problem(
        DateTime date, TimeSpan start, int visitMinutes, CalendarInputs inputs,
        IEnumerable<BookingSlot> doctorBookings, DateTime clinicNow, int? excludeReservationId = null)
    {
        if (visitMinutes < MinVisitMinutes || visitMinutes > MaxVisitMinutes)
            return $"The visit length must be between {MinVisitMinutes} and {MaxVisitMinutes} minutes.";

        var visit = new TimePeriod(start, start + TimeSpan.FromMinutes(visitMinutes));
        if (visit.End > TimeSpan.FromDays(1))
            return "The visit must end on the same day.";

        if (date.Date < clinicNow.Date || (date.Date == clinicNow.Date && start < clinicNow.TimeOfDay))
            return "Choose a current or future time; past times can't be booked.";

        var others = doctorBookings.Where(b => b.ReservationId != excludeReservationId && b.Date.Date == date.Date).ToList();

        if (!Availability.Fits(date, visit, inputs))
        {
            var free = Availability.FreeStarts(date, visitMinutes, inputs, others, clinicNow).Take(6).ToList();
            var hint = free.Count == 0
                ? " There are no free times that day."
                : $" Free times that day: {string.Join(", ", free.Select(Time))}.";
            return $"{Time(start)} is outside the doctor's hours in this clinic on {Day(date)}.{hint}";
        }

        var clash = others.Where(b => b.Period.Overlaps(visit)).OrderBy(b => b.Start).FirstOrDefault();
        if (clash is not null)
            return $"The doctor already has a booking from {Time(clash.Start)} to {Time(clash.Period.End)} on {Day(date)} (#{clash.ReservationId}). Choose another time.";

        return null;
    }

    private static string Time(TimeSpan t) => t.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    private static string Day(DateTime d) => d.ToString("ddd dd MMM yyyy", CultureInfo.InvariantCulture);
}
