using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.Common.Auditing;
using SafyaClinic.Application.DTOs.MedicalRecord;
using SafyaClinic.Application.DTOs.Payment;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Entities.Payment;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>F3b-2: which record fields changed; payment cancel/change are audited.</summary>
public class AuditRecordsAndPaymentsTests
{
    private sealed class FixedContext : IAuditContext
    {
        public int? UserId => null;
        public string? IpAddress => "10.0.0.2";
        public string? RequestId => "REQ-2";
    }

    // ── Which fields changed (pure) ─────────────────────────

    [Fact]
    public void Unchanged_edit_reports_no_fields()
    {
        var current = new UpdatePatientRecordRequest { Diagnosis = "Flu", Notes = "Rest" };

        Assert.Empty(MedicalRecordChanges.ChangedFields(current, new UpdatePatientRecordRequest { Diagnosis = "Flu", Notes = "Rest" }));
    }

    [Fact]
    public void Changed_fields_are_named_and_whitespace_is_ignored()
    {
        var current = new UpdatePatientRecordRequest { Diagnosis = "Flu", Notes = "Rest", FollowUpDate = new DateTime(2026, 12, 1) };
        var update = new UpdatePatientRecordRequest { Diagnosis = "Influenza", Notes = "  Rest ", FollowUpDate = new DateTime(2026, 12, 8) };

        Assert.Equal(new[] { "Diagnosis", "FollowUpDate" }, MedicalRecordChanges.ChangedFields(current, update));
    }

    // ── Payments (in memory) ────────────────────────────────

    private static async Task<(SafyaDbContext Context, int AdminId, int PaymentId)> SeedPaymentAsync()
    {
        var context = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"audit-pay-{Guid.NewGuid()}").Options);
        await DbSeeder.SeedAsync(context, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "Audit-Pay-Password-1" })
            .Build());

        var adminId = await context.Users.Where(u => u.PhoneNumber == "0100").Select(u => u.Id).SingleAsync();
        var patient = new Patient { FirstName = "Test", LastName = "Patient" };
        context.Patients.Add(patient);
        await context.SaveChangesAsync();
        var payment = new Payment
        {
            PatientId = patient.Id,
            CollectedBy = adminId,
            Amount = 500m,
            OriginalAmount = 500m,
            ClinicNetAmount = 500m,
            Status = PaymentStatusEnum.Active
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return (context, adminId, payment.Id);
    }

    private static PaymentService Payments(SafyaDbContext context)
    {
        context.ChangeTracker.Clear();
        var uow = new UnitOfWork(context);
        return new PaymentService(uow, new AuditTrail(uow, new FixedContext()));
    }

    [Fact]
    public async Task Cancelled_payment_is_audited()
    {
        var (context, adminId, paymentId) = await SeedPaymentAsync();

        await Payments(context).CancelPaymentAsync(new CancelPaymentRequest { PaymentId = paymentId, Reason = "Duplicate" }, adminId);

        var entry = Assert.Single(await context.AuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal(AuditActions.PaymentCancelled, entry.Action);
        Assert.Equal(paymentId, entry.EntityId);
        Assert.Equal(adminId, entry.UserId);
        Assert.Contains("500", entry.Details);
    }

    [Fact]
    public async Task Changed_amount_is_audited_with_old_and_new_amounts()
    {
        var (context, adminId, paymentId) = await SeedPaymentAsync();

        await Payments(context).ChangePaymentAmountAsync(
            new ChangePaymentAmountRequest { PaymentId = paymentId, NewAmount = 450m, Reason = "Discount" }, adminId);

        var entry = Assert.Single(await context.AuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal(AuditActions.PaymentAmountChanged, entry.Action);
        Assert.Contains("500", entry.Details);
        Assert.Contains("450", entry.Details);
    }
}