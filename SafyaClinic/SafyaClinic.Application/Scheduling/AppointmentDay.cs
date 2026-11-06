namespace SafyaClinic.Application.Scheduling;

/// <summary>P2b: one booking on the appointments screen.</summary>
public sealed record AppointmentDto(int ReservationId, TimeSpan Start, int DurationMinutes, string PatientName, string Status)
{
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
