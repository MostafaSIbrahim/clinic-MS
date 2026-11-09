using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.Common.Auditing;
using SafyaClinic.Application.DTOs.Payment;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Entities.Payment;
using SafyaClinic.Domain.Entities.Reservation;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>
/// P6b: a write-off excuses (part of) a visit's balance, with a reason. It covers the visit, is never revenue,
/// is audited and can be undone.
/// </summary>
public class WriteOffTests
{
    private sealed class NoContext : IAuditContext
    {
        public int? UserId => null;
        public string? IpAddress => null;
        public string? RequestId => null;
    }

    private sealed record World(SafyaDbContext Db, int Admin, int Patient, int Reservation);

    private static async Task<World> WorldAsync(decimal price = 800m, decimal collected = 500m,
        PatientQueueStatus queue = PatientQueueStatus.Finished)
    {
        var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"writeoff-{Guid.NewGuid()}").Options);
        await DbSeeder.SeedAsync(db, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "WriteOff-Test-Password-1" })
            .Build());
        var admin = await db.Users.Where(u => u.PhoneNumber == "0100").Select(u => u.Id).SingleAsync();
        var confirmed = await db.ReservationStatuses.Where(s => s.StatusName == "Confirmed").Select(s => s.Id).SingleAsync();
        var patient = new Patient { FirstName = "Mona", LastName = "Adel" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        var reservation = new Reservation
        {
            PatientId = patient.Id, DoctorId = admin, ClinicId = 1, StatusId = confirmed, TreatmentTypeId = 1,
            ReservationDate = new DateTime(2026, 11, 9), ReservationTime = new TimeSpan(10, 0, 0), DurationMinutes = 30,
            TotalAmount = price, QueueStatus = queue,
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();
        if (collected > 0)
            db.Payments.Add(new Payment
            {
                PatientId = patient.Id, ReservationId = reservation.Id, ClinicId = 1, CollectedBy = admin,
                Amount = collected, OriginalAmount = collected, ClinicNetAmount = collected, Status = PaymentStatusEnum.Active,
                PaymentDate = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new World(db, admin, patient.Id, reservation.Id);
    }

    private static PaymentService Payments(World w)
    {
        w.Db.ChangeTracker.Clear();
        var uow = new UnitOfWork(w.Db);
        return new PaymentService(uow, new AuditTrail(uow, new NoContext()));
    }

    private static WriteOffRequest Request(World w, decimal amount, string reason = "Discount", string? note = null) =>
        new() { PatientId = w.Patient, ReservationId = w.Reservation, Amount = amount, Reason = reason, Note = note };

    private static async Task<decimal> DueAsync(World w) =>
        (await Payments(w).GetDueAmountAsync(w.Patient, w.Reservation, null)).Data;

    private static Task<bool> IsPaidAsync(World w) =>
        w.Db.Reservations.AsNoTracking().Where(r => r.Id == w.Reservation).Select(r => r.IsPaid).SingleAsync();

    [Fact]
    public async Task Writing_off_the_rest_covers_the_visit_but_is_not_revenue()
    {
        var w = await WorldAsync();

        var result = await Payments(w).WriteOffAsync(Request(w, 300m, "Hardship"), w.Admin);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, await DueAsync(w));
        Assert.True(await IsPaidAsync(w));
        var writeOff = await w.Db.Payments.AsNoTracking().SingleAsync(p => p.Status == PaymentStatusEnum.WrittenOff);
        Assert.Equal(300m, writeOff.Amount);
        Assert.Equal(WriteOffReason.Hardship, writeOff.WriteOffReason);
        Assert.Equal(PaymentMethodEnum.WriteOff, writeOff.PaymentMethod);
        Assert.Equal(0m, writeOff.ClinicNetAmount);
        var revenue = (await Payments(w).GetRevenueBetweenAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1))).Data;
        Assert.Equal(500m, revenue);   // only the money collected
    }

    [Fact]
    public async Task A_write_off_is_audited_and_kept_in_the_payment_history()
    {
        var w = await WorldAsync();

        await Payments(w).WriteOffAsync(Request(w, 100m, "Other", "Long-standing patient"), w.Admin);

        var entry = Assert.Single(await w.Db.AuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal(AuditActions.PaymentWrittenOff, entry.Action);
        Assert.Contains("Other", entry.Details);
        var adjustment = Assert.Single(await w.Db.PaymentAdjustments.AsNoTracking().ToListAsync());
        Assert.Equal("WrittenOff", adjustment.ActionType);
        Assert.Contains("Long-standing patient", adjustment.Reason);
        Assert.Equal(200m, await DueAsync(w));   // a partial write-off leaves the rest to collect
    }

    [Theory]
    [InlineData(300, "", null)]          // no reason
    [InlineData(300, "Generosity", null)] // unknown reason
    [InlineData(300, "Other", " ")]      // Other without a note
    [InlineData(301, "Discount", null)]  // more than the balance
    [InlineData(0, "Discount", null)]    // nothing
    public async Task Invalid_write_offs_are_refused_and_change_nothing(decimal amount, string reason, string? note)
    {
        var w = await WorldAsync();

        var result = await Payments(w).WriteOffAsync(Request(w, amount, reason, note), w.Admin);

        Assert.False(result.IsSuccess);
        Assert.False(await w.Db.Payments.AnyAsync(p => p.Status == PaymentStatusEnum.WrittenOff));
        Assert.Equal(300m, await DueAsync(w));
    }

    [Fact]
    public async Task A_visit_without_a_balance_cannot_be_written_off()
    {
        var notStarted = await WorldAsync(queue: PatientQueueStatus.Waiting);   // no balance before consultation
        Assert.False((await Payments(notStarted).WriteOffAsync(Request(notStarted, 100m), notStarted.Admin)).IsSuccess);

        var paid = await WorldAsync(collected: 800m);
        Assert.False((await Payments(paid).WriteOffAsync(Request(paid, 1m), paid.Admin)).IsSuccess);
    }

    [Fact]
    public async Task Collect_never_accepts_the_write_off_method()
    {
        var w = await WorldAsync();

        var result = await Payments(w).CollectPaymentAsync(new CollectPaymentRequest
        {
            PatientId = w.Patient, ReservationId = w.Reservation, ClinicId = 1, Amount = 300m, PaymentMethod = "WriteOff",
        }, w.Admin);

        Assert.False(result.IsSuccess);
        Assert.Equal(300m, await DueAsync(w));
    }

    [Fact]
    public async Task Undoing_a_write_off_makes_the_amount_due_again()
    {
        var w = await WorldAsync();
        var writeOff = (await Payments(w).WriteOffAsync(Request(w, 300m), w.Admin)).Data!;

        var result = await Payments(w).CancelWriteOffAsync(new CancelWriteOffRequest { PaymentId = writeOff.Id, Reason = "Entered for the wrong visit" }, w.Admin);

        Assert.True(result.IsSuccess);
        Assert.Equal(300m, await DueAsync(w));
        Assert.False(await IsPaidAsync(w));
        Assert.Contains(await w.Db.AuditLogs.AsNoTracking().Select(a => a.Action).ToListAsync(), a => a == AuditActions.PaymentWriteOffCancelled);
        Assert.False((await Payments(w).CancelWriteOffAsync(new CancelWriteOffRequest { PaymentId = writeOff.Id, Reason = "again" }, w.Admin)).IsSuccess);
    }

    [Fact]
    public async Task The_dashboard_shows_write_offs_by_reason_apart_from_collected_money()
    {
        var w = await WorldAsync();
        await Payments(w).WriteOffAsync(Request(w, 200m, "StaffOrFamily"), w.Admin);
        await Payments(w).WriteOffAsync(Request(w, 100m, "Discount"), w.Admin);

        var dashboard = (await Payments(w).GetPaymentDashboardAsync()).Data!;

        Assert.Equal(300m, dashboard.TotalWrittenOff);
        Assert.Equal(new[] { "StaffOrFamily", "Discount" }, dashboard.WrittenOffByReason.Select(r => r.Reason));
        Assert.Equal(500m, dashboard.AmountByClinic.Sum(c => c.TotalCollected));
    }
}
