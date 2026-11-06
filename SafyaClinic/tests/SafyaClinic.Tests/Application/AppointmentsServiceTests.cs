using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Common;
using SafyaClinic.Application.Common.Auditing;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Entities.Reservation;
using SafyaClinic.Domain.Entities.Scheduling;
using SafyaClinic.Domain.Entities.Settings;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>P2b: the appointments day shows who works in the clinic, their bookings and their free times.</summary>
public class AppointmentsServiceTests
{
    private static TimeSpan At(int hour, int minute = 0) => new(hour, minute, 0);

    // Saturday 7 Nov 2026; "now" is Friday 6 Nov 2026 12:00 clinic time.
    private static readonly DateTime Sat = new(2026, 11, 7);

    private sealed record World(SafyaDbContext Db, int Karim, int Mona, int Main, int Branch, int Patient);

    private static async Task<World> WorldAsync()
    {
        var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"appointments-{Guid.NewGuid()}").Options);
        var karim = new User { FullName = "Dr Karim", PhoneNumber = "0401", PasswordHash = "x", ClinicianType = ClinicianType.Doctor };
        var mona = new User { FullName = "Dr Mona", PhoneNumber = "0402", PasswordHash = "x", ClinicianType = ClinicianType.Doctor };
        var desk = new User { FullName = "Front desk", PhoneNumber = "0403", PasswordHash = "x" };
        var main = new Clinic { Name = "Main" };
        var branch = new Clinic { Name = "Nasr City" };
        var patient = new Patient { FirstName = "Mona", LastName = "Adel" };
        db.AddRange(karim, mona, desk, main, branch, patient,
            new ReservationStatus { StatusName = "Confirmed" }, new ReservationStatus { StatusName = "Cancelled" });
        await db.SaveChangesAsync();
        foreach (var clinic in new[] { main, branch })
            db.ClinicOpeningHours.Add(new ClinicOpeningHours { ClinicId = clinic.Id, DayOfWeek = DayOfWeek.Saturday, OpensAt = At(10), ClosesAt = At(12) });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new World(db, karim.Id, mona.Id, main.Id, branch.Id, patient.Id);
    }

    private static async Task BookAsync(World w, int doctorId, int clinicId, TimeSpan at, string status = "Confirmed")
    {
        var statusId = await w.Db.ReservationStatuses.Where(s => s.StatusName == status).Select(s => s.Id).SingleAsync();
        w.Db.Reservations.Add(new Reservation
        {
            PatientId = w.Patient, DoctorId = doctorId, ClinicId = clinicId, StatusId = statusId, TreatmentTypeId = 1,
            ReservationDate = Sat, ReservationTime = at, DurationMinutes = 30,
        });
        await w.Db.SaveChangesAsync();
        w.Db.ChangeTracker.Clear();
    }

    private static AppointmentsService Service(World w)
    {
        w.Db.ChangeTracker.Clear();
        var clock = new ClinicClock(ClinicClock.FindZone(ClinicClock.DefaultTimeZoneId), () => new DateTime(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc));
        return new AppointmentsService(new UnitOfWork(w.Db), clock);
    }

    private static string Times(IEnumerable<TimeSpan> starts) => string.Join(" ", starts.Select(s => s.ToString(@"hh\:mm")));

    [Fact]
    public async Task Doctors_without_a_calendar_are_offered_in_clinic_hours_with_their_bookings_shown()
    {
        var w = await WorldAsync();
        await BookAsync(w, w.Karim, w.Main, At(10, 30));

        var day = (await Service(w).GetDayAsync(Sat, w.Main, 30)).Data!;

        var karim = day.Doctors.Single(d => d.DoctorId == w.Karim);
        var booking = Assert.Single(karim.Bookings);
        Assert.Equal("Mona Adel", booking.PatientName);
        Assert.Equal(w.Patient, booking.PatientId);   // P2d: coverage badge and payment summary link
        Assert.False(booking.IsPaid);
        Assert.Equal("10:00 11:00 11:15 11:30", Times(karim.FreeStarts));
        Assert.Equal(2, day.Doctors.Count);
        Assert.Empty(day.NotHere);
    }

    [Fact]
    public async Task A_booking_in_another_clinic_takes_the_time_here_too()
    {
        var w = await WorldAsync();
        await BookAsync(w, w.Karim, w.Branch, At(10));

        var karim = (await Service(w).GetDayAsync(Sat, w.Main, 30)).Data!.Doctors.Single(d => d.DoctorId == w.Karim);

        Assert.Empty(karim.Bookings);   // that booking belongs to the other clinic's day
        Assert.Equal("10:30 10:45 11:00 11:15 11:30", Times(karim.FreeStarts));
    }

    [Fact]
    public async Task A_doctor_whose_calendar_is_not_in_use_here_is_listed_as_not_here()
    {
        var w = await WorldAsync();
        w.Db.DoctorSchedules.Add(new DoctorSchedule { DoctorId = w.Mona, ClinicId = w.Main, Mode = ScheduleMode.Open, StepMinutes = 15, IsActive = false });
        await w.Db.SaveChangesAsync();

        var day = (await Service(w).GetDayAsync(Sat, w.Main, 30)).Data!;

        Assert.DoesNotContain(day.Doctors, d => d.DoctorId == w.Mona);
        Assert.Equal(new[] { "Dr Mona" }, day.NotHere);
    }

    [Fact]
    public async Task Cancelled_bookings_are_neither_shown_nor_holding_a_time()
    {
        var w = await WorldAsync();
        await BookAsync(w, w.Karim, w.Main, At(10), status: "Cancelled");

        var karim = (await Service(w).GetDayAsync(Sat, w.Main, 30)).Data!.Doctors.Single(d => d.DoctorId == w.Karim);

        Assert.Empty(karim.Bookings);
        Assert.StartsWith("10:00", Times(karim.FreeStarts));
    }

    [Fact]
    public async Task A_closed_day_offers_nobody_and_a_past_day_offers_no_free_times()
    {
        var w = await WorldAsync();
        Assert.Empty((await Service(w).GetDayAsync(Sat.AddDays(2), w.Main, 30)).Data!.Doctors);   // Monday: closed

        await BookAsync(w, w.Karim, w.Main, At(10));
        var lastWeek = new DateTime(2026, 10, 31);
        var day = (await Service(w).GetDayAsync(lastWeek, w.Main, 30)).Data!;
        Assert.All(day.Doctors, d => Assert.Empty(d.FreeStarts));
    }

    // ── Move (P2c) ──────────────────────────────────────────────

    private static ReservationService Reservations(World w, IAuditTrail? audit = null)
    {
        w.Db.ChangeTracker.Clear();
        var clock = new ClinicClock(ClinicClock.FindZone(ClinicClock.DefaultTimeZoneId), () => new DateTime(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc));
        return new ReservationService(new UnitOfWork(w.Db), new SafyaClinic.Infrastructure.Security.PermissionStore(w.Db), clock, audit);
    }

    private static Task<int> IdAtAsync(World w, TimeSpan at) =>
        w.Db.Reservations.Where(r => r.ReservationTime == at).Select(r => r.Id).SingleAsync();

    private sealed class NoContext : IAuditContext
    {
        public int? UserId => 1;
        public string? IpAddress => null;
        public string? RequestId => null;
    }

    [Fact]
    public async Task Moving_offers_the_doctors_free_times_without_counting_the_booking_itself()
    {
        var w = await WorldAsync();
        await BookAsync(w, w.Karim, w.Main, At(10));

        var options = (await Service(w).GetMoveAsync(await IdAtAsync(w, At(10)), date: null, clinicId: null)).Data!;

        Assert.Null(options.NotMovable);
        Assert.Equal("10:15 10:30 10:45 11:00 11:15 11:30", Times(options.FreeStarts));   // its own 10:00 isn't offered
    }

    [Fact]
    public async Task A_booking_moves_to_a_free_time_and_the_move_is_audited()
    {
        var w = await WorldAsync();
        await BookAsync(w, w.Karim, w.Main, At(10));
        var id = await IdAtAsync(w, At(10));
        var uow = new UnitOfWork(w.Db);

        var result = await Reservations(w, new AuditTrail(uow, new NoContext())).MoveReservationAsync(id, w.Branch, Sat, At(11), 1);

        Assert.True(result.IsSuccess);
        var moved = await w.Db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id);
        Assert.Equal((w.Branch, At(11)), (moved.ClinicId, moved.ReservationTime));
        Assert.True(await w.Db.AuditLogs.AnyAsync(a => a.Action == AuditActions.ReservationMoved && a.EntityId == id));
    }

    [Fact]
    public async Task A_booking_cannot_move_onto_another_booking_or_outside_the_hours()
    {
        var w = await WorldAsync();
        await BookAsync(w, w.Karim, w.Main, At(10));
        await BookAsync(w, w.Karim, w.Main, At(11));
        var id = await IdAtAsync(w, At(10));

        Assert.Contains("already has a booking", (await Reservations(w).MoveReservationAsync(id, w.Main, Sat, At(11, 15), 1)).Errors.First());
        Assert.Contains("outside", (await Reservations(w).MoveReservationAsync(id, w.Main, Sat, At(15), 1)).Errors.First());
    }

    [Fact]
    public async Task Cancelled_or_checked_in_bookings_cannot_be_moved()
    {
        var w = await WorldAsync();
        await BookAsync(w, w.Karim, w.Main, At(10), status: "Cancelled");
        await BookAsync(w, w.Mona, w.Main, At(10));
        var checkedIn = await w.Db.Reservations.SingleAsync(r => r.DoctorId == w.Mona);
        checkedIn.QueueStatus = PatientQueueStatus.Waiting;
        await w.Db.SaveChangesAsync();

        Assert.False((await Reservations(w).MoveReservationAsync(await w.Db.Reservations.Where(r => r.DoctorId == w.Karim).Select(r => r.Id).SingleAsync(), w.Main, Sat, At(11), 1)).IsSuccess);
        Assert.False((await Reservations(w).MoveReservationAsync(checkedIn.Id, w.Main, Sat, At(11), 1)).IsSuccess);
        var monaToday = (await Service(w).GetDayAsync(Sat, w.Main, 30)).Data!.Doctors.Single(d => d.DoctorId == w.Mona);
        Assert.Equal(PatientQueueStatus.Waiting, Assert.Single(monaToday.Bookings).QueueStatus);   // P4: shown instead of Check in
        Assert.NotNull((await Service(w).GetMoveAsync(checkedIn.Id, null, null)).Data!.NotMovable);
    }
}
