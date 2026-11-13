using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Common.Auditing;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>M4a: nutrition enrollment status rules, and no balance owed on a cancelled enrollment.</summary>
public class EnrollmentRulesTests
{
    // ── The rules (pure) ────────────────────────────────────

    [Theory]
    [InlineData(EnrollmentStatus.Active, EnrollmentStatus.OnHold, true)]
    [InlineData(EnrollmentStatus.Active, EnrollmentStatus.Cancelled, true)]
    [InlineData(EnrollmentStatus.OnHold, EnrollmentStatus.Active, true)]
    [InlineData(EnrollmentStatus.OnHold, EnrollmentStatus.Cancelled, true)]
    [InlineData(EnrollmentStatus.Active, EnrollmentStatus.Active, true)]          // no change
    [InlineData(EnrollmentStatus.Active, EnrollmentStatus.Completed, false)]      // only automatically
    [InlineData(EnrollmentStatus.OnHold, EnrollmentStatus.Completed, false)]
    [InlineData(EnrollmentStatus.Completed, EnrollmentStatus.Active, false)]      // final
    [InlineData(EnrollmentStatus.Completed, EnrollmentStatus.Cancelled, false)]
    [InlineData(EnrollmentStatus.Cancelled, EnrollmentStatus.Active, false)]
    [InlineData(EnrollmentStatus.Cancelled, EnrollmentStatus.OnHold, false)]
    public void Only_the_allowed_status_changes_pass(EnrollmentStatus from, EnrollmentStatus to, bool allowed)
    {
        Assert.Equal(allowed, EnrollmentRules.CheckStatusChange(from, to) is null);
    }

    [Fact]
    public void The_offered_changes_are_exactly_the_allowed_ones()
    {
        foreach (var from in Enum.GetValues<EnrollmentStatus>())
            foreach (var to in Enum.GetValues<EnrollmentStatus>())
                if (from != to)
                    Assert.Equal(EnrollmentRules.CheckStatusChange(from, to) is null, EnrollmentRules.NextStatuses(from).Contains(to));
    }

    // ── The service follows them (in memory) ────────────────

    private sealed class Recorder : IAuditTrail
    {
        public List<(string Action, int? Id, object? Details)> Entries { get; } = new();
        public Task RecordAsync(string action, string entityType, int? entityId, object? details = null, int? actingUserId = null)
        {
            Entries.Add((action, entityId, details));
            return Task.CompletedTask;
        }
    }

    private sealed record World(SafyaDbContext Db, int Patient, int Package, int Doctor);

    private static async Task<World> WorldAsync()
    {
        var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"enrollment-{Guid.NewGuid()}").Options);
        var doctor = new User { FullName = "Dr Salma", PhoneNumber = "0402", PasswordHash = "x", ClinicianType = ClinicianType.Doctor };
        var patient = new Patient { FirstName = "Mona", LastName = "Adel" };
        var package = new NutritionPackage { PackageName = "Monthly", BasePrice = 1000m, MaxDiscountPercent = 20m };
        db.AddRange(doctor, patient, package);
        await db.SaveChangesAsync();
        return new World(db, patient.Id, package.Id, doctor.Id);
    }

    private static async Task<int> EnrollAsync(World w, EnrollmentStatus status, decimal paid = 0m)
    {
        var e = new PatientNutritionEnrollment
        {
            PatientId = w.Patient, PackageId = w.Package, DoctorId = w.Doctor, BasePrice = 1000m, DiscountPercent = 10m,
            StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 29), Status = status, TotalPaid = paid,
        };
        w.Db.PatientNutritionEnrollments.Add(e);
        await w.Db.SaveChangesAsync();
        w.Db.ChangeTracker.Clear();
        return e.Id;
    }

    private static NutritionService Nutrition(World w, IAuditTrail audit)
    {
        w.Db.ChangeTracker.Clear();
        return new NutritionService(new UnitOfWork(w.Db), audit);
    }

    private static Task<EnrollmentStatus> StatusAsync(World w, int id) =>
        w.Db.PatientNutritionEnrollments.AsNoTracking().Where(e => e.Id == id).Select(e => e.Status).SingleAsync();

    [Fact]
    public async Task Putting_on_hold_and_resuming_are_saved_and_audited()
    {
        var w = await WorldAsync();
        var id = await EnrollAsync(w, EnrollmentStatus.Active);
        var audit = new Recorder();

        Assert.True((await Nutrition(w, audit).UpdateEnrollmentStatusAsync(id, "OnHold")).IsSuccess);
        Assert.Equal(EnrollmentStatus.OnHold, await StatusAsync(w, id));
        Assert.True((await Nutrition(w, audit).UpdateEnrollmentStatusAsync(id, "Active")).IsSuccess);
        Assert.Equal(EnrollmentStatus.Active, await StatusAsync(w, id));

        Assert.Equal(2, audit.Entries.Count);
        Assert.All(audit.Entries, e => Assert.Equal(AuditActions.EnrollmentStatusChanged, e.Action));
        Assert.All(audit.Entries, e => Assert.Equal(id, e.Id));
    }

    [Theory]
    [InlineData(EnrollmentStatus.Completed, "Active")]
    [InlineData(EnrollmentStatus.Cancelled, "Active")]
    [InlineData(EnrollmentStatus.Cancelled, "OnHold")]
    [InlineData(EnrollmentStatus.Active, "Completed")]
    [InlineData(EnrollmentStatus.Active, "5")]          // not a status
    [InlineData(EnrollmentStatus.Active, "Paused")]
    public async Task Refused_changes_change_nothing_and_say_why(EnrollmentStatus current, string target)
    {
        var w = await WorldAsync();
        var id = await EnrollAsync(w, current);
        var audit = new Recorder();

        var result = await Nutrition(w, audit).UpdateEnrollmentStatusAsync(id, target);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(current, await StatusAsync(w, id));
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task Resuming_is_refused_while_another_enrollment_is_active()
    {
        var w = await WorldAsync();
        var onHold = await EnrollAsync(w, EnrollmentStatus.OnHold);
        await EnrollAsync(w, EnrollmentStatus.Active);

        var result = await Nutrition(w, new Recorder()).UpdateEnrollmentStatusAsync(onHold, "Active");

        Assert.False(result.IsSuccess);
        Assert.Contains(EnrollmentRules.AnotherActiveMessage, result.Errors);
        Assert.Equal(EnrollmentStatus.OnHold, await StatusAsync(w, onHold));
    }

    // ── Decision 1: nothing more is owed on a cancelled enrollment ──

    private static PaymentService Payments(World w)
    {
        w.Db.ChangeTracker.Clear();
        return new PaymentService(new UnitOfWork(w.Db));
    }

    [Fact]
    public async Task A_cancelled_enrollment_owes_nothing_more()
    {
        var w = await WorldAsync();
        var cancelled = await EnrollAsync(w, EnrollmentStatus.Cancelled, paid: 300m);   // final price 900

        var due = await Payments(w).GetDueAmountAsync(w.Patient, null, cancelled);
        var summary = (await Payments(w).GetPatientFinancialSummaryAsync(w.Patient)).Data!;

        Assert.Equal(0m, due.Data);
        Assert.DoesNotContain(summary.PendingPayments, p => p.EnrollmentId == cancelled);
        Assert.Equal(300m, summary.TotalCharged);   // only what was already paid
    }

    [Fact]
    public async Task The_amount_due_is_in_whole_piastres_so_the_Collect_form_accepts_it()
    {
        // 333.33 × (1 − 12%) = 293.3304; 850.00 × (1 − 12%) = 748.0000. The form allows two decimals.
        var w = await WorldAsync();
        var odd = new PatientNutritionEnrollment
        {
            PatientId = w.Patient, PackageId = w.Package, DoctorId = w.Doctor, BasePrice = 333.33m, DiscountPercent = 12m,
            StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 29), Status = EnrollmentStatus.Active,
        };
        w.Db.PatientNutritionEnrollments.Add(odd);
        await w.Db.SaveChangesAsync();

        var due = (await Payments(w).GetDueAmountAsync(w.Patient, null, odd.Id)).Data;
        var pending = Assert.Single((await Payments(w).GetPatientFinancialSummaryAsync(w.Patient)).Data!.PendingPayments);

        Assert.Equal(293.33m, due);
        Assert.True(due.Scale <= 2, $"{due} has {due.Scale} decimals");
        Assert.Equal(due, pending.AmountDue);
        Assert.Equal(748.00m, SafyaClinic.Application.Common.Money.Round(850.00m * (1 - 12.00m / 100)));
        Assert.Equal(2, SafyaClinic.Application.Common.Money.Round(850.00m * (1 - 12.00m / 100)).Scale);
    }

    [Fact]
    public async Task An_active_enrollment_still_owes_its_balance()
    {
        var w = await WorldAsync();
        var active = await EnrollAsync(w, EnrollmentStatus.Active, paid: 300m);

        var due = await Payments(w).GetDueAmountAsync(w.Patient, null, active);
        var summary = (await Payments(w).GetPatientFinancialSummaryAsync(w.Patient)).Data!;

        Assert.Equal(600m, due.Data);
        Assert.Equal(600m, Assert.Single(summary.PendingPayments, p => p.EnrollmentId == active).AmountDue);
        Assert.Equal(900m, summary.TotalCharged);
    }
}
