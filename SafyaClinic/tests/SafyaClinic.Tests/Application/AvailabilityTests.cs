using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Common;
using SafyaClinic.Application.Scheduling;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Reservation;
using SafyaClinic.Domain.Entities.Settings;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>P1c: when a doctor can be booked, free start times, date changes and the booked-time guard.</summary>
public class AvailabilityTests
{
    private static TimeSpan At(int hour, int minute = 0) => new(hour, minute, 0);

    // Saturday 7 Nov 2026 (a Saturday); "now" is Friday 6 Nov 2026 12:00 clinic time unless a test says otherwise.
    private static readonly DateTime Sat = new(2026, 11, 7);
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0);

    private static readonly OpeningPeriodDto[] Hours =
    {
        new(DayOfWeek.Saturday, At(10), At(14)), new(DayOfWeek.Saturday, At(17), At(21)),
    };

    private static ScheduleTimeDto W(int from, int to) => new(DayOfWeek.Saturday, ScheduleTimeKind.Working, At(from), At(to));
    private static ScheduleTimeDto B(int from, int fromMin, int to, int toMin) => new(DayOfWeek.Saturday, ScheduleTimeKind.Break, At(from, fromMin), At(to, toMin));
    private static ScheduleTimeDto S(int from, int to) => new(DayOfWeek.Saturday, ScheduleTimeKind.Slot, At(from), At(to));

    private static DoctorScheduleDto Open(int step, params ScheduleTimeDto[] t) => new(1, 1, ScheduleMode.Open, step, true, t);
    private static CalendarInputs With(DoctorScheduleDto? s, params DateChangeDto[] changes) => new(Hours, s, changes);
    private static string Times(IEnumerable<TimeSpan> starts) => string.Join(" ", starts.Select(s => s.ToString(@"hh\:mm")));

    // ── Bookable periods ────────────────────────────────────────

    [Fact]
    public void Without_a_calendar_the_clinic_hours_apply_with_a_15_minute_step()
    {
        var (periods, step) = Availability.For(Sat, With(null));

        Assert.Equal(new[] { "10:00–14:00", "17:00–21:00" }, periods.Select(p => p.ToString()));
        Assert.Equal(15, step);
    }

    [Fact]
    public void An_open_calendar_is_its_working_periods_minus_breaks_inside_clinic_hours()
    {
        var (periods, _) = Availability.For(Sat, With(Open(15, W(9, 13), B(11, 0, 11, 30))));

        Assert.Equal(new[] { "10:00–11:00", "11:30–13:00" }, periods.Select(p => p.ToString()));
    }

    [Fact]
    public void A_calendar_not_in_use_and_a_closed_day_are_not_bookable()
    {
        Assert.Empty(Availability.For(Sat, With(Open(15, W(10, 14)) with { IsActive = false })).Periods);
        Assert.Empty(Availability.For(Sat.AddDays(2), With(null)).Periods);   // Monday: clinic closed
    }

    [Fact]
    public void A_date_change_replaces_the_weekly_pattern_for_that_date_only()
    {
        var schedule = Open(15, W(10, 14));
        var dayOff = new DateChangeDto(Sat, true, Array.Empty<ScheduleTimeDto>());
        var evening = new DateChangeDto(Sat, false, new[] { W(17, 19) });

        Assert.Empty(Availability.For(Sat, With(schedule, dayOff)).Periods);
        Assert.Equal(new[] { "17:00–19:00" }, Availability.For(Sat, With(schedule, evening)).Periods.Select(p => p.ToString()));
        Assert.Equal(new[] { "10:00–14:00" }, Availability.For(Sat.AddDays(7), With(schedule, evening)).Periods.Select(p => p.ToString()));
    }

    // ── Free start times ────────────────────────────────────────

    [Fact]
    public void Free_starts_follow_the_step_and_the_whole_visit_must_fit()
    {
        var starts = Availability.FreeStarts(Sat, 30, With(new DoctorScheduleDto(1, 1, ScheduleMode.SpecificSlots, 15, true, new[] { S(10, 11) })),
            Array.Empty<BookingSlot>(), Now);

        Assert.Equal("10:00 10:15 10:30", Times(starts));
    }

    [Fact]
    public void A_visit_overlapping_any_of_the_doctors_bookings_is_not_offered()
    {
        var booked = new BookingSlot(5, 1, 2, Sat, At(10, 30), 30);   // in another clinic: still the same doctor

        var starts = Availability.FreeStarts(Sat, 30, With(new DoctorScheduleDto(1, 1, ScheduleMode.SpecificSlots, 15, true, new[] { S(10, 12) })),
            new[] { booked }, Now);

        Assert.Equal("10:00 11:00 11:15 11:30", Times(starts));
    }

    [Fact]
    public void Past_dates_and_past_times_today_are_never_offered()
    {
        var calendar = With(null);

        Assert.Empty(Availability.FreeStarts(Sat.AddDays(-7), 30, calendar, Array.Empty<BookingSlot>(), Now));   // last Saturday
        var today = Availability.FreeStarts(Sat, 60, calendar, Array.Empty<BookingSlot>(), Sat.AddHours(19).AddMinutes(10));
        Assert.Equal("19:15 19:30 19:45 20:00", Times(today));
    }

    // ── Guard ───────────────────────────────────────────────────

    [Fact]
    public void A_change_that_removes_a_booked_time_strands_that_booking_only()
    {
        var before = With(Open(15, W(10, 14)));
        var after = With(Open(15, W(10, 12)));
        var inside = new BookingSlot(1, 1, 1, Sat, At(10), 30);
        var removed = new BookingSlot(2, 1, 1, Sat, At(12, 30), 30);
        var alreadyOutside = new BookingSlot(3, 1, 1, Sat, At(18), 30);   // never fitted: not reported

        var stranded = Availability.Stranded(before, after, new[] { inside, removed, alreadyOutside });

        Assert.Equal(new[] { 2 }, stranded.Select(b => b.ReservationId));
    }

    // ── Services with the database ──────────────────────────────

    private sealed record World(SafyaDbContext Db, int DoctorId, int ClinicId, IClinicClock Clock);

    private static async Task<World> WorldAsync()
    {
        var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"availability-{Guid.NewGuid()}").Options);
        var doctor = new User { FullName = "Dr Karim", PhoneNumber = "0101", PasswordHash = "x", ClinicianType = ClinicianType.Doctor };
        var clinic = new Clinic { Name = "Main" };
        db.AddRange(doctor, clinic,
            new ReservationStatus { StatusName = "Confirmed" }, new ReservationStatus { StatusName = "Cancelled" });
        await db.SaveChangesAsync();
        db.ClinicOpeningHours.AddRange(
            new ClinicOpeningHours { ClinicId = clinic.Id, DayOfWeek = DayOfWeek.Saturday, OpensAt = At(10), ClosesAt = At(14) },
            new ClinicOpeningHours { ClinicId = clinic.Id, DayOfWeek = DayOfWeek.Saturday, OpensAt = At(17), ClosesAt = At(21) });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var clock = new ClinicClock(ClinicClock.FindZone(ClinicClock.DefaultTimeZoneId), () => new DateTime(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc));
        return new World(db, doctor.Id, clinic.Id, clock);
    }

    private static async Task BookAsync(World w, TimeSpan at, string status = "Confirmed")
    {
        var statusId = await w.Db.ReservationStatuses.Where(s => s.StatusName == status).Select(s => s.Id).SingleAsync();
        w.Db.Reservations.Add(new Reservation
        {
            PatientId = 1, DoctorId = w.DoctorId, ClinicId = w.ClinicId, StatusId = statusId, TreatmentTypeId = 1,
            ReservationDate = Sat, ReservationTime = at, DurationMinutes = 30,
        });
        await w.Db.SaveChangesAsync();
        w.Db.ChangeTracker.Clear();
    }

    private static DoctorScheduleService Schedules(World w)
    {
        w.Db.ChangeTracker.Clear();
        return new DoctorScheduleService(new UnitOfWork(w.Db), audit: null, w.Clock);
    }

    private static ClinicHoursService Hours_(World w)
    {
        w.Db.ChangeTracker.Clear();
        return new ClinicHoursService(new UnitOfWork(w.Db), audit: null, w.Clock);
    }

    private static DoctorScheduleDto Weekly(World w, params ScheduleTimeDto[] t) => new(w.DoctorId, w.ClinicId, ScheduleMode.Open, 15, true, t);

    [Fact]
    public async Task Setting_a_first_calendar_that_leaves_out_a_booked_time_is_refused_and_names_the_booking()
    {
        var w = await WorldAsync();
        await BookAsync(w, At(18));   // fits the clinic hours (no calendar yet)

        var result = await Schedules(w).SaveAsync(Weekly(w, W(10, 14)), 1);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("Sat 07 Nov 2026 18:00"));
        Assert.True((await Schedules(w).SaveAsync(Weekly(w, W(10, 14), W(17, 21)), 1)).IsSuccess);
    }

    [Fact]
    public async Task Cancelled_bookings_do_not_block_changes()
    {
        var w = await WorldAsync();
        await BookAsync(w, At(18), status: "Cancelled");

        Assert.True((await Schedules(w).SaveAsync(Weekly(w, W(10, 14)), 1)).IsSuccess);
    }

    [Fact]
    public async Task A_day_off_on_a_booked_date_is_refused_until_the_booking_is_moved()
    {
        var w = await WorldAsync();
        await Schedules(w).SaveAsync(Weekly(w, W(10, 14)), 1);
        await BookAsync(w, At(11));

        var dayOff = await Schedules(w).SetDateChangeAsync(w.DoctorId, w.ClinicId, new DateChangeDto(Sat, true, Array.Empty<ScheduleTimeDto>()), 1);
        Assert.False(dayOff.IsSuccess);

        var nextWeek = await Schedules(w).SetDateChangeAsync(w.DoctorId, w.ClinicId, new DateChangeDto(Sat.AddDays(7), true, Array.Empty<ScheduleTimeDto>()), 1);
        Assert.True(nextWeek.IsSuccess);
        Assert.Empty((await Schedules(w).FreeStartsAsync(w.DoctorId, w.ClinicId, Sat.AddDays(7), 30)).Data!);
    }

    [Fact]
    public async Task A_date_change_replaces_that_date_and_can_be_removed_again()
    {
        var w = await WorldAsync();
        await Schedules(w).SaveAsync(Weekly(w, W(10, 14)), 1);

        await Schedules(w).SetDateChangeAsync(w.DoctorId, w.ClinicId, new DateChangeDto(Sat, false, new[] { W(17, 18) }), 1);
        await Schedules(w).SetDateChangeAsync(w.DoctorId, w.ClinicId, new DateChangeDto(Sat, false, new[] { W(17, 19) }), 1);
        var view = (await Schedules(w).GetAsync(w.DoctorId, w.ClinicId)).Data!;
        var change = Assert.Single(view.DateChanges);
        Assert.Equal(new[] { W(17, 19) }, change.Times);
        Assert.Equal("17:00 17:15 17:30 17:45 18:00 18:15 18:30",
            Times((await Schedules(w).FreeStartsAsync(w.DoctorId, w.ClinicId, Sat, 30)).Data!));

        Assert.True((await Schedules(w).RemoveDateChangeAsync(w.DoctorId, w.ClinicId, Sat, 1)).IsSuccess);
        Assert.Empty((await Schedules(w).GetAsync(w.DoctorId, w.ClinicId)).Data!.DateChanges);
    }

    [Fact]
    public async Task Date_changes_need_a_saved_calendar_a_future_date_and_valid_times()
    {
        var w = await WorldAsync();
        var none = await Schedules(w).SetDateChangeAsync(w.DoctorId, w.ClinicId, new DateChangeDto(Sat, true, Array.Empty<ScheduleTimeDto>()), 1);
        Assert.False(none.IsSuccess);

        await Schedules(w).SaveAsync(Weekly(w, W(10, 14)), 1);
        Assert.False((await Schedules(w).SetDateChangeAsync(w.DoctorId, w.ClinicId, new DateChangeDto(Sat.AddDays(-7), true, Array.Empty<ScheduleTimeDto>()), 1)).IsSuccess);
        Assert.False((await Schedules(w).SetDateChangeAsync(w.DoctorId, w.ClinicId, new DateChangeDto(Sat, false, new[] { W(7, 9) }), 1)).IsSuccess);
    }

    [Fact]
    public async Task Clinic_hours_that_would_leave_out_a_booking_are_refused()
    {
        var w = await WorldAsync();
        await BookAsync(w, At(18));   // the doctor has no calendar: clinic hours apply

        var morningOnly = await Hours_(w).SaveAsync(w.ClinicId, new[] { new OpeningPeriodDto(DayOfWeek.Saturday, At(10), At(14)) }, 1);

        Assert.False(morningOnly.IsSuccess);
        Assert.Contains(morningOnly.Errors, e => e.Contains("18:00"));
    }
}
