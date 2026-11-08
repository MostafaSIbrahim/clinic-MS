using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.Billing;
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
/// P6a: one coverage rule. Active (collected) and WrittenOff (excused) payments cover a visit;
/// a Cancelled payment is a void, so the balance reopens.
/// </summary>
public class CoverageTests
{
    // ── The rule (pure) ─────────────────────────────────────

    [Theory]
    [InlineData(PaymentStatusEnum.Active, true)]
    [InlineData(PaymentStatusEnum.WrittenOff, true)]
    [InlineData(PaymentStatusEnum.Cancelled, false)]
    public void Collected_and_written_off_cover_a_visit_voids_do_not(PaymentStatusEnum status, bool covers)
    {
        Assert.Equal(covers, Coverage.CoversStatus(status));
        Assert.Equal(covers, Coverage.CoversStatus(status.ToString()));
        Assert.Equal(covers, Coverage.Covers.Compile()(new Payment { Status = status }));
    }

    [Fact]
    public void Unknown_status_text_does_not_cover() => Assert.False(Coverage.CoversStatus("Refunded"));

    [Theory]
    [InlineData(800, 0, 800, false)]
    [InlineData(800, 500, 300, false)]
    [InlineData(800, 800, 0, true)]
    [InlineData(800, 900, 0, true)]
    [InlineData(0, 0, 0, true)]
    public void Due_and_covered_always_agree(decimal price, decimal covered, decimal due, bool isCovered)
    {
        Assert.Equal(due, Coverage.Due(price, covered));
        Assert.Equal(isCovered, Coverage.IsCovered(price, covered));
        Assert.Equal(isCovered, Coverage.Due(price, covered) == 0m);   // for any set price
    }

    [Fact]
    public void A_visit_without_a_price_owes_nothing_but_is_not_marked_covered()
    {
        Assert.Equal(0m, Coverage.Due(null, 0m));
        Assert.False(Coverage.IsCovered(null, 0m));
    }

    // ── The services use it (in memory) ─────────────────────

    private sealed class NoContext : IAuditContext
    {
        public int? UserId => null;
        public string? IpAddress => null;
        public string? RequestId => null;
    }

    private sealed record World(SafyaDbContext Db, int Admin, int Patient, int Reservation);

    /// <summary>A finished visit priced at <paramref name="price"/>, with the given payments.</summary>
    private static async Task<World> WorldAsync(decimal price, params (decimal Amount, PaymentStatusEnum Status)[] payments)
    {
        var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"coverage-{Guid.NewGuid()}").Options);
        await DbSeeder.SeedAsync(db, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "Coverage-Test-Password-1" })
            .Build());
        var admin = await db.Users.Where(u => u.PhoneNumber == "0100").Select(u => u.Id).SingleAsync();
        var confirmed = await db.ReservationStatuses.Where(s => s.StatusName == "Confirmed").Select(s => s.Id).SingleAsync();
        var patient = new Patient { FirstName = "Mona", LastName = "Adel" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        var reservation = new Reservation
        {
            PatientId = patient.Id, DoctorId = admin, ClinicId = 1, StatusId = confirmed, TreatmentTypeId = 1,
            ReservationDate = new DateTime(2026, 11, 8), ReservationTime = new TimeSpan(10, 0, 0), DurationMinutes = 30,
            TotalAmount = price, QueueStatus = PatientQueueStatus.Finished,
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();
        foreach (var (amount, status) in payments)
            db.Payments.Add(new Payment
            {
                PatientId = patient.Id, ReservationId = reservation.Id, ClinicId = 1, CollectedBy = admin,
                Amount = amount, OriginalAmount = amount, ClinicNetAmount = amount, Status = status,
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

    private static Task<decimal> DueAsync(World w) =>
        Payments(w).GetDueAmountAsync(w.Patient, w.Reservation, null).ContinueWith(t => t.Result.Data);

    [Fact]
    public async Task Collected_plus_written_off_settles_the_visit()
    {
        var w = await WorldAsync(800m, (500m, PaymentStatusEnum.Active), (300m, PaymentStatusEnum.WrittenOff));

        Assert.Equal(0m, await DueAsync(w));
    }

    [Fact]
    public async Task A_cancelled_payment_does_not_cover_the_visit()
    {
        var w = await WorldAsync(800m, (500m, PaymentStatusEnum.Active), (300m, PaymentStatusEnum.Cancelled));

        Assert.Equal(300m, await DueAsync(w));
    }

    [Fact]
    public async Task Cancelling_a_payment_reopens_the_balance_and_clears_covered()
    {
        var w = await WorldAsync(500m, (500m, PaymentStatusEnum.Active));
        var paymentId = await w.Db.Payments.Select(p => p.Id).SingleAsync();
        var reservation = await w.Db.Reservations.SingleAsync(r => r.Id == w.Reservation);
        reservation.IsPaid = true;
        await w.Db.SaveChangesAsync();
        Assert.Equal(0m, await DueAsync(w));

        var result = await Payments(w).CancelPaymentAsync(new CancelPaymentRequest { PaymentId = paymentId, Reason = "Typed by mistake" }, w.Admin);

        Assert.True(result.IsSuccess);
        Assert.Equal(500m, await DueAsync(w));
        Assert.False(await w.Db.Reservations.AsNoTracking().Where(r => r.Id == w.Reservation).Select(r => r.IsPaid).SingleAsync());
    }

    [Fact]
    public async Task A_write_off_cannot_be_cancelled_or_changed_by_payment_adjustments()
    {
        var w = await WorldAsync(800m, (800m, PaymentStatusEnum.WrittenOff));
        var paymentId = await w.Db.Payments.Select(p => p.Id).SingleAsync();

        var cancel = await Payments(w).CancelPaymentAsync(new CancelPaymentRequest { PaymentId = paymentId, Reason = "x" }, w.Admin);
        var change = await Payments(w).ChangePaymentAmountAsync(new ChangePaymentAmountRequest { PaymentId = paymentId, NewAmount = 100m, Reason = "x" }, w.Admin);

        Assert.False(cancel.IsSuccess);
        Assert.False(change.IsSuccess);
        Assert.Equal(PaymentStatusEnum.WrittenOff, await w.Db.Payments.AsNoTracking().Select(p => p.Status).SingleAsync());
    }

    [Fact]
    public async Task The_patient_summary_shows_write_offs_not_voids_as_written_off()
    {
        var w = await WorldAsync(800m, (400m, PaymentStatusEnum.Active), (100m, PaymentStatusEnum.WrittenOff), (300m, PaymentStatusEnum.Cancelled));

        var summary = (await Payments(w).GetPatientFinancialSummaryAsync(w.Patient)).Data!;

        Assert.Equal(400m, summary.TotalPaid);
        Assert.Equal(100m, summary.TotalWrittenOff);
        var pending = Assert.Single(summary.PendingPayments);
        Assert.Equal(300m, pending.AmountDue);   // the same figure as the Collect form
        Assert.Equal(await DueAsync(w), pending.AmountDue);
    }
}
