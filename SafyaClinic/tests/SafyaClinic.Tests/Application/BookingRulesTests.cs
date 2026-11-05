using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.Common;
using SafyaClinic.Application.DTOs.Reservation;
using SafyaClinic.Application.Scheduling;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Entities.Settings;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using SafyaClinic.Infrastructure.Security;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>P2: a reservation may only take a current or future time inside the doctor's hours, never twice.</summary>
public class BookingRulesTests
{
    private static TimeSpan At(int hour, int minute = 0) => new(hour, minute, 0);

    // Saturday 7 Nov 2026; "now" is Friday 6 Nov 2026 12:00 clinic time.
    private static readonly DateTime Sat = new(2026, 11, 7);
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0);
    private static readonly CalendarInputs SaturdayMornings =
        new(new[] { new OpeningPeriodDto(DayOfWeek.Saturday, At(10), At(14)) }, null, Array.Empty<DateChangeDto>());
    private static readonly CalendarInputs NoHoursSet = new(Array.Empty<OpeningPeriodDto>(), null, Array.Empty<DateChangeDto>());

    private static string? Check(TimeSpan at, int minutes = 30, CalendarInputs? inputs = null, DateTime? date = null,
        IEnumerable<BookingSlot>? bookings = null, int? exclude = null, DateTime? now = null) =>
        BookingRules.Problem(date ?? Sat, at, minutes, inputs ?? SaturdayMornings, bookings ?? Array.Empty<BookingSlot>(), now ?? Now, exclude);

    // ── Rules ───────────────────────────────────────────────────

    [Fact]
    public void A_free_time_inside_the_hours_can_be_booked()
    {
        Assert.Null(Check(At(10)));
        Assert.Null(Check(At(13, 30)));
    }

    [Fact]
    public void Past_dates_and_past_times_today_are_refused()
    {
        Assert.Contains("past", Check(At(11), date: Sat.AddDays(-7)));
        Assert.Contains("past", Check(At(11), now: Sat.AddHours(11).AddMinutes(5)));
        Assert.Null(Check(At(11, 30), now: Sat.AddHours(11).AddMinutes(5)));
    }

    [Fact]
    public void Outside_the_hours_is_refused_and_the_message_offers_free_times()
    {
        var problem = Check(At(13, 45));   // 13:45–14:15 runs past closing

        Assert.Contains("outside the doctor's hours", problem);
        Assert.Contains("Free times that day: 10:00, 10:15", problem);
    }

    [Fact]
    public void The_doctor_cannot_be_booked_twice_at_the_same_time_but_a_reservation_may_keep_its_own_time()
    {
        var existing = new BookingSlot(41, 1, 2, Sat, At(10, 30), 30);

        Assert.Contains("already has a booking", Check(At(10, 15), bookings: new[] { existing }));
        Assert.Null(Check(At(10, 30), bookings: new[] { existing }, exclude: 41));
        Assert.Null(Check(At(11), bookings: new[] { existing }));   // touching is fine
    }

    [Theory]
    [InlineData(0)]
    [InlineData(600)]
    public void The_visit_length_must_be_sensible(int minutes)
    {
        Assert.Contains("visit length", Check(At(10), minutes));
    }

    [Fact]
    public void A_clinic_without_any_opening_hours_is_not_restricted_yet()
    {
        Assert.Null(Check(At(7), inputs: NoHoursSet));
        Assert.Null(Check(At(22), inputs: NoHoursSet, date: Sat.AddDays(2)));
        Assert.Contains("same day", Check(At(23, 45), inputs: NoHoursSet));
    }

    // ── Reservation service ─────────────────────────────────────

    private sealed record World(SafyaDbContext Db, int ReceptionId, int DoctorId, int OtherDoctorId, int PatientId, int ClinicId, int TypeId);

    private static async Task<World> WorldAsync()
    {
        var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"booking-{Guid.NewGuid()}").Options);
        await DbSeeder.SeedAsync(db, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "Booking-Test-Password-1" })
            .Build());

        async Task<int> UserAsync(string name, string phone, string role, ClinicianType type)
        {
            var user = new User { FullName = name, PhoneNumber = phone, PasswordHash = "x", SecurityStamp = "s", ClinicianType = type };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            await TestData.AddToBuiltInGroupAsync(db, user.Id, role);
            return user.Id;
        }

        var reception = await UserAsync("Reception", "0301", "Reception", ClinicianType.None);
        var doctor = await UserAsync("Dr Karim", "0302", "Doctor", ClinicianType.Doctor);
        var other = await UserAsync("Dr Mona", "0303", "Doctor", ClinicianType.Doctor);
        var patient = new Patient { FirstName = "Test", LastName = "Patient" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        var clinicId = await db.Clinics.Select(c => c.Id).FirstAsync();
        db.ClinicOpeningHours.Add(new ClinicOpeningHours { ClinicId = clinicId, DayOfWeek = DayOfWeek.Saturday, OpensAt = At(10), ClosesAt = At(14) });
        await db.SaveChangesAsync();
        var typeId = await db.TreatmentTypes.OrderBy(t => t.Id).Select(t => t.Id).FirstAsync();
        db.ChangeTracker.Clear();
        return new World(db, reception, doctor, other, patient.Id, clinicId, typeId);
    }

    private static ReservationService Service(World w)
    {
        w.Db.ChangeTracker.Clear();   // each call is a new request in the app
        var clock = new ClinicClock(ClinicClock.FindZone(ClinicClock.DefaultTimeZoneId), () => new DateTime(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc));
        return new ReservationService(new UnitOfWork(w.Db), new PermissionStore(w.Db), clock);
    }

    private static CreateReservationRequest At(World w, TimeSpan time, int? doctorId = null) => new()
    {
        PatientId = w.PatientId,
        DoctorId = doctorId ?? w.DoctorId,
        ClinicId = w.ClinicId,
        TreatmentTypeId = w.TypeId,
        Category = "InternalMedicine",
        ReservationDate = Sat,
        ReservationTime = time,
        DurationMinutes = 30,
    };

    [Fact]
    public async Task Booking_a_free_time_works_and_the_same_time_cannot_be_booked_again()
    {
        var w = await WorldAsync();

        Assert.True((await Service(w).CreateReservationAsync(At(w, At(10)), w.ReceptionId)).IsSuccess);
        var twice = await Service(w).CreateReservationAsync(At(w, At(10, 15)), w.ReceptionId);

        Assert.False(twice.IsSuccess);
        Assert.Contains("already has a booking", twice.Errors.First());
        Assert.Equal(1, await w.Db.Reservations.CountAsync());
    }

    [Fact]
    public async Task Another_doctor_can_be_booked_at_the_same_time()
    {
        var w = await WorldAsync();
        await Service(w).CreateReservationAsync(At(w, At(10)), w.ReceptionId);

        Assert.True((await Service(w).CreateReservationAsync(At(w, At(10), w.OtherDoctorId), w.ReceptionId)).IsSuccess);
    }

    [Fact]
    public async Task Booking_outside_the_clinic_hours_is_refused()
    {
        var w = await WorldAsync();

        var result = await Service(w).CreateReservationAsync(At(w, At(16)), w.ReceptionId);

        Assert.False(result.IsSuccess);
        Assert.Contains("outside the doctor's hours", result.Errors.First());
        Assert.Equal(0, await w.Db.Reservations.CountAsync());
    }
}
