using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.DTOs.Payment;
using SafyaClinic.Application.Services;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>
/// M2a: the payment dashboard adds up in the database now. On the same data it must give exactly the figures the
/// old in-memory version gave: every total, unpaid row and group, for the full and the front-desk view, with and
/// without dates.
/// </summary>
public class PaymentDashboardEquivalenceTests
{
    private static async Task<SafyaDbContext> WorldAsync()
    {
        var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"dashboard-{Guid.NewGuid()}").Options);
        await DbSeeder.SeedAsync(db, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "Dashboard-Test-Password-1" })
            .Build());
        await DashboardTestData.SeedAsync(db, reservations: 600);
        return db;
    }

    public static IEnumerable<object?[]> Ranges() => new[]
    {
        new object?[] { null, null, false },
        new object?[] { null, null, true },
        new object?[] { new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), false },
        new object?[] { new DateTime(2025, 11, 15), null, false },
        new object?[] { null, new DateTime(2026, 3, 1), false },
        new object?[] { new DateTime(2030, 1, 1), new DateTime(2030, 1, 31), false },   // nothing in range
    };

    [Theory]
    [MemberData(nameof(Ranges))]
    public async Task The_database_version_gives_the_same_figures_as_before(DateTime? from, DateTime? to, bool unpaidOnly)
    {
        var db = await WorldAsync();

        db.ChangeTracker.Clear();
        var before = await LegacyPaymentDashboard.ComputeAsync(new UnitOfWork(db), from, to, unpaidOnly);
        db.ChangeTracker.Clear();
        var after = await new PaymentService(new UnitOfWork(db)).GetPaymentDashboardAsync(from, to, unpaidOnly);

        Assert.True(before.IsSuccess && after.IsSuccess);
        Assert.Equal(DashboardTestData.Figures(before.Data!), DashboardTestData.Figures(after.Data!));
    }

    [Fact]
    public async Task The_test_data_covers_every_case_the_dashboard_distinguishes()
    {
        var db = await WorldAsync();

        var d = (await new PaymentService(new UnitOfWork(db)).GetPaymentDashboardAsync()).Data!;

        Assert.NotEmpty(d.UnpaidCompletedReservations);
        Assert.NotEmpty(d.UnpaidPendingReservations);
        Assert.True(d.FullyPaidPaymentCount > 0);
        Assert.True(d.TotalWrittenOff > 0);
        Assert.Contains(d.WrittenOffByReason, r => r.Reason == "Before P6 (cancelled payment)");
        Assert.Contains(d.AmountByClinic, c => c.ClinicId is null);
        Assert.Contains(d.AmountBySource, s => s.PatientSourceId is null);
    }

    [Fact]
    public async Task Unpaid_lists_come_newest_first()
    {
        var db = await WorldAsync();

        var d = (await new PaymentService(new UnitOfWork(db)).GetPaymentDashboardAsync(unpaidOnly: true)).Data!;

        var dates = d.UnpaidCompletedReservations.Select(r => r.ReservationDate).ToList();
        Assert.Equal(dates.OrderByDescending(x => x), dates);
    }
}
