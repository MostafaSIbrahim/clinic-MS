using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.DTOs.Reservation;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Entities.Reservation;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>
/// S7: the service applies D4/D6 with roles read from the database.
/// Refusals happen before the SQL-Server-only status update, so they run in memory.
/// </summary>
public class ReservationServiceRulesTests
{
    private sealed record Setup(
        SafyaDbContext Context, int AdminId, int ReceptionId, int DoctorAId, int DoctorBId,
        int PatientId, int ClinicId, int TreatmentTypeId, decimal DefaultCost, int ReservationOfDoctorA);

    private static async Task<int> AddUserAsync(SafyaDbContext context, string name, string phone, string role)
    {
        var user = new User { FullName = name, PhoneNumber = phone, PasswordHash = "x", SecurityStamp = "s" };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var roleId = await context.Roles.Where(r => r.RoleName == role).Select(r => r.Id).SingleAsync();
        context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
        await context.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<Setup> SeedAsync()
    {
        var context = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"reservation-rules-{Guid.NewGuid()}")
            .Options);
        await DbSeeder.SeedAsync(context, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "Rules-Test-Password-1" })
            .Build());

        var adminId = await context.Users.Where(u => u.PhoneNumber == "0100").Select(u => u.Id).SingleAsync();
        var receptionId = await AddUserAsync(context, "Reception", "0201", "Reception");
        var doctorAId = await AddUserAsync(context, "Doctor A", "0202", "Doctor");
        var doctorBId = await AddUserAsync(context, "Doctor B", "0203", "Doctor");

        var patient = new Patient { FirstName = "Test", LastName = "Patient" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();

        var clinicId = await context.Clinics.Select(c => c.Id).FirstAsync();
        var type = await context.TreatmentTypes.Where(t => t.DefaultCost > 0).OrderBy(t => t.Id).FirstAsync();

        var reservation = new Reservation
        {
            PatientId = patient.Id,
            DoctorId = doctorAId,
            ClinicId = clinicId,
            TreatmentTypeId = type.Id,
            StatusId = await context.ReservationStatuses.Where(s => s.StatusName == "Confirmed").Select(s => s.Id).SingleAsync(),
            ReservationDate = DateTime.Today,
            ReservationTime = new TimeSpan(10, 0, 0),
            TotalAmount = type.DefaultCost
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return new Setup(context, adminId, receptionId, doctorAId, doctorBId, patient.Id, clinicId, type.Id,
            type.DefaultCost!.Value, reservation.Id);
    }

    private static ReservationService Service(SafyaDbContext context)
    {
        context.ChangeTracker.Clear();   // each call is a new request in the app
        return new ReservationService(new UnitOfWork(context));
    }

    private CreateReservationRequest Booking(Setup s, decimal amount) => new()
    {
        PatientId = s.PatientId,
        DoctorId = s.DoctorAId,
        ClinicId = s.ClinicId,
        TreatmentTypeId = s.TreatmentTypeId,
        ReservationDate = DateTime.Today.AddDays(1),
        ReservationTime = new TimeSpan(11, 0, 0),
        TotalAmount = amount
    };

    [Fact]
    public async Task Doctor_cannot_change_the_status_of_another_doctors_reservation()
    {
        var s = await SeedAsync();
        var cancelled = await s.Context.ReservationStatuses.Where(x => x.StatusName == "Cancelled").Select(x => x.Id).SingleAsync();

        var result = await Service(s.Context).UpdateStatusAsync(s.ReservationOfDoctorA, cancelled, s.DoctorBId);

        Assert.False(result.IsSuccess);
        Assert.Contains("another doctor", result.Errors.First());
    }

    [Fact]
    public async Task Doctor_cannot_cancel_another_doctors_reservation()
    {
        var s = await SeedAsync();

        var result = await Service(s.Context).CancelReservationAsync(s.ReservationOfDoctorA, s.DoctorBId, "test");

        Assert.False(result.IsSuccess);
        Assert.Contains("another doctor", result.Errors.First());
    }

    [Fact]
    public async Task Reception_price_is_replaced_by_the_treatment_type_price()
    {
        var s = await SeedAsync();

        await Service(s.Context).CreateReservationAsync(Booking(s, amount: 1m), s.ReceptionId);

        var stored = await s.Context.Reservations.AsNoTracking()
            .Where(r => r.Id != s.ReservationOfDoctorA).Select(r => r.TotalAmount).SingleAsync();
        Assert.Equal(s.DefaultCost, stored);
    }

    [Fact]
    public async Task Admin_price_override_is_kept()
    {
        var s = await SeedAsync();

        await Service(s.Context).CreateReservationAsync(Booking(s, amount: 1m), s.AdminId);

        var stored = await s.Context.Reservations.AsNoTracking()
            .Where(r => r.Id != s.ReservationOfDoctorA).Select(r => r.TotalAmount).SingleAsync();
        Assert.Equal(1m, stored);
    }
}