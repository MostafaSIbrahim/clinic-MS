namespace SafyaClinic.Application.Common;

/// <summary>
/// The clinic's local time (F2). Storage stays in UTC; "today", day ranges and
/// every time shown to staff go through this clock.
/// </summary>
public interface IClinicClock
{
    TimeZoneInfo TimeZone { get; }
    DateTime UtcNow { get; }

    /// <summary>Current clinic local time.</summary>
    DateTime Now { get; }

    /// <summary>Current clinic calendar date (time 00:00).</summary>
    DateTime Today { get; }

    /// <summary>Converts a stored UTC instant to clinic local time.</summary>
    DateTime ToClinicTime(DateTime utc);

    /// <summary>The UTC instant at which a clinic calendar date starts.</summary>
    DateTime DayStartUtc(DateTime clinicDate);
}

public sealed class ClinicClock : IClinicClock
{
    public const string TimeZoneSettingKey = "Clinic:TimeZone";
    public const string DefaultTimeZoneId = "Africa/Cairo";

    private readonly Func<DateTime> _utcNow;

    public ClinicClock(TimeZoneInfo timeZone, Func<DateTime>? utcNow = null)
    {
        TimeZone = timeZone;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>Fallback for code paths without dependency injection (e.g. tests).</summary>
    public static ClinicClock Cairo { get; } = new(FindZone(DefaultTimeZoneId));

    /// <summary>Accepts IANA ids ("Africa/Cairo") on every OS, falling back to the Windows id.</summary>
    public static TimeZoneInfo FindZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException) when (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId!);
        }
    }

    public TimeZoneInfo TimeZone { get; }

    public DateTime UtcNow => DateTime.SpecifyKind(_utcNow(), DateTimeKind.Utc);

    public DateTime Now => ToClinicTime(UtcNow);

    public DateTime Today => Now.Date;

    public DateTime ToClinicTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);

    public DateTime DayStartUtc(DateTime clinicDate)
    {
        // On a spring-forward day midnight may not exist; use the first real instant of the date.
        var local = DateTime.SpecifyKind(clinicDate.Date, DateTimeKind.Unspecified);
        while (TimeZone.IsInvalidTime(local)) local = local.AddMinutes(1);
        if (TimeZone.IsAmbiguousTime(local))
            return new DateTimeOffset(local, TimeZone.GetAmbiguousTimeOffsets(local).Max()).UtcDateTime;
        return TimeZoneInfo.ConvertTimeToUtc(local, TimeZone);
    }
}