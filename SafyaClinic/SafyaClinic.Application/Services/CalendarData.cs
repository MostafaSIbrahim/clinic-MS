using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Scheduling;
using SafyaClinic.Domain.Entities.Scheduling;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Interfaces.Repositories;

namespace SafyaClinic.Application.Services;

/// <summary>P1c: loads what <see cref="Availability"/> needs from the database.</summary>
internal static class CalendarData
{
    /// <summary>Bookings in these statuses no longer hold a time.</summary>
    private static readonly string[] Released = { "Cancelled", "NoShow", "Completed" };

    public static async Task<IReadOnlyList<OpeningPeriodDto>> ClinicHoursAsync(IUnitOfWork uow, int clinicId)
    {
        var rows = await uow.ClinicOpeningHours.Query()
            .Where(h => h.ClinicId == clinicId)
            .Select(h => new OpeningPeriodDto(h.DayOfWeek, h.OpensAt, h.ClosesAt))
            .ToListAsync();
        return OpeningHoursRules.Sorted(rows);
    }

    /// <summary>The doctor's calendar in the clinic (null when none is set) and its date changes from a date on.</summary>
    public static async Task<(DoctorScheduleDto? Schedule, IReadOnlyList<DateChangeDto> Changes)> ScheduleAsync(
        IUnitOfWork uow, int doctorId, int clinicId, DateTime fromDate)
    {
        var schedule = await uow.DoctorSchedules.Query()
            .Include(s => s.Times)
            .Include(s => s.Changes)
            .FirstOrDefaultAsync(s => s.DoctorId == doctorId && s.ClinicId == clinicId);
        if (schedule is null)
            return (null, Array.Empty<DateChangeDto>());
        return (ToDto(schedule), Changes(schedule.Changes.Where(c => c.Date >= fromDate.Date)));
    }

    public static async Task<CalendarInputs> InputsAsync(IUnitOfWork uow, int doctorId, int clinicId, DateTime fromDate)
    {
        var (schedule, changes) = await ScheduleAsync(uow, doctorId, clinicId, fromDate);
        return new CalendarInputs(await ClinicHoursAsync(uow, clinicId), schedule, changes);
    }

    /// <summary>Bookings that still hold a time, from a date on; optionally one doctor and/or one clinic.</summary>
    public static async Task<IReadOnlyList<BookingSlot>> BookingsAsync(IUnitOfWork uow, DateTime fromDate, int? doctorId, int? clinicId, DateTime? toDate = null)
    {
        var from = fromDate.Date;
        var to = toDate?.Date;
        return await uow.Reservations.Query()
            .Where(r => r.ReservationDate >= from
                        && (to == null || r.ReservationDate <= to)
                        && (doctorId == null || r.DoctorId == doctorId)
                        && (clinicId == null || r.ClinicId == clinicId)
                        && !Released.Contains(r.Status.StatusName))
            .Select(r => new BookingSlot(r.Id, r.DoctorId, r.ClinicId, r.ReservationDate, r.ReservationTime, r.DurationMinutes))
            .ToListAsync();
    }

    /// <summary>Of the bookings given, those that hold a time later than now (clinic time).</summary>
    public static IEnumerable<BookingSlot> Upcoming(IEnumerable<BookingSlot> bookings, DateTime clinicNow) =>
        bookings.Where(b => b.Date.Date > clinicNow.Date || (b.Date.Date == clinicNow.Date && b.Start >= clinicNow.TimeOfDay));

    public static DoctorScheduleDto ToDto(DoctorSchedule s) =>
        new(s.DoctorId, s.ClinicId, s.Mode, s.StepMinutes, s.IsActive,
            s.Times.Select(t => new ScheduleTimeDto(t.DayOfWeek, t.Kind, t.StartTime, t.EndTime))
                .OrderBy(t => ClinicWeek.Position(t.Day)).ThenBy(t => t.Kind).ThenBy(t => t.Start)
                .ToList());

    public static IReadOnlyList<DateChangeDto> Changes(IEnumerable<DoctorScheduleChange> rows) =>
        rows.GroupBy(c => c.Date.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DateChangeDto(g.Key, g.Any(c => c.IsDayOff),
                g.Where(c => !c.IsDayOff && c.Kind is not null && c.StartTime is not null && c.EndTime is not null)
                    .Select(c => new ScheduleTimeDto(g.Key.DayOfWeek, c.Kind!.Value, c.StartTime!.Value, c.EndTime!.Value))
                    .OrderBy(t => t.Kind).ThenBy(t => t.Start)
                    .ToList()))
            .ToList();

    /// <summary>"Move or cancel these bookings first: #12 Sat 14 Nov 2026 10:00; …"</summary>
    public static string StrandedMessage(IEnumerable<BookingSlot> stranded) =>
        "This change would leave bookings outside the doctor's hours. Move or cancel them first: "
        + string.Join("; ", stranded.Select(Availability.Describe)) + ".";
}
