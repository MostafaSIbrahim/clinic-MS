namespace SafyaClinic.Application.Scheduling;

/// <summary>P2b: one booking on the appointments screen.</summary>
public sealed record AppointmentDto(int ReservationId, TimeSpan Start, int DurationMinutes, string PatientName, string Status)
{
    /// <summary>P2d: for the coverage badge and the payment summary link.</summary>
    public int PatientId { get; init; }
    public bool IsPaid { get; init; }

    public TimeSpan End => Start + TimeSpan.FromMinutes(DurationMinutes);
}

/// <summary>P2b: one doctor's column for a day in a clinic: their bookings there and their free start times.</summary>
public sealed record DoctorDayDto(
    int DoctorId, string DoctorName, bool HasCalendar, IReadOnlyList<TimePeriod> Hours,
    IReadOnlyList<AppointmentDto> Bookings, IReadOnlyList<TimeSpan> FreeStarts);

/// <summary>P2b: a clinic's day: the doctors working there and those who aren't.</summary>
public sealed record AppointmentDayDto(
    DateTime Date, int ClinicId, string ClinicName, bool ClinicHasHours, int VisitMinutes,
    IReadOnlyList<DoctorDayDto> Doctors, IReadOnlyList<string> NotHere);

/// <summary>P2c: moving a booking: where it is now, and the doctor's free times on the chosen date and clinic.</summary>
public sealed record MoveOptionsDto(
    int ReservationId, string PatientName, int DoctorId, string DoctorName,
    int CurrentClinicId, string CurrentClinicName, DateTime CurrentDate, TimeSpan CurrentTime, int DurationMinutes,
    DateTime Date, int ClinicId, string ClinicName, IReadOnlyList<TimeSpan> FreeStarts, string? NotMovable);
