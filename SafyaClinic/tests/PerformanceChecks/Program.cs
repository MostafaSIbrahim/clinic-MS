using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Payment;
using SafyaClinic.Application.Services;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using SafyaClinic.Tests.Application;

// M2a performance check (CI job "performance", report-only). Against a real SQL Server whose schema was built
// by the reviewed migration script: seeds N reservations (default 50,000) with payments, then times the payment
// dashboard (full, this month, front desk). Target: p95 under 1 second. It also checks that the database
// version gives the same figures as the pre-M2 version on this data. Connection string: PERF_DB.
var connection = Environment.GetEnvironmentVariable("PERF_DB")
    ?? throw new InvalidOperationException("Set PERF_DB to the test database's connection string.");
var count = args.Length > 0 ? int.Parse(args[0]) : 50_000;
const int runs = 10;
const double targetMs = 1000;

var options = new DbContextOptionsBuilder<SafyaDbContext>()
    .UseSqlServer(connection, sql => sql.CommandTimeout(300)).Options;

await using (var db = new SafyaDbContext(options))
{
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
    Console.WriteLine($"Migrations not applied by the script: {pending.Count}");
    if (pending.Count > 0) { Console.Error.WriteLine(string.Join(", ", pending)); return 1; }

    await DbSeeder.SeedAsync(db, new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "Perf-" + Guid.NewGuid() })
        .Build());
    var seeding = Stopwatch.StartNew();
    await DashboardTestData.SeedAsync(db, count, batch: 5000);
    Console.WriteLine($"Seeded {count:N0} reservations and {await db.Payments.CountAsync():N0} payments in {seeding.Elapsed.TotalSeconds:N0} s");
}

var month = (From: new DateTime(2026, 3, 1), To: new DateTime(2026, 3, 31));
var scenarios = new (string Name, DateTime? From, DateTime? To, bool UnpaidOnly)[]
{
    ("Full dashboard, all time", null, null, false),
    ("Full dashboard, one month", month.From, month.To, false),
    ("Front desk (unpaid only)", null, null, true),
};

var report = new StringBuilder();
report.AppendLine($"### Payment dashboard with {count:N0} reservations");
report.AppendLine();
report.AppendLine("| View | Before M2, one run | Now, median | Now, p95 | Same figures | Within 1 s |");
report.AppendLine("|---|---|---|---|---|---|");
var failed = false;

foreach (var s in scenarios)
{
    async Task<(double Ms, ServiceResult<PaymentDashboardDto> Result)> TimeAsync(bool legacy)
    {
        await using var db = new SafyaDbContext(options);
        var uow = new UnitOfWork(db);
        var watch = Stopwatch.StartNew();
        var result = legacy
            ? await LegacyPaymentDashboard.ComputeAsync(uow, s.From, s.To, s.UnpaidOnly)
            : await new PaymentService(uow).GetPaymentDashboardAsync(s.From, s.To, s.UnpaidOnly);
        return (watch.Elapsed.TotalMilliseconds, result);
    }

    var (legacyMs, before) = await TimeAsync(legacy: true);
    await TimeAsync(legacy: false);   // warm-up
    var times = new List<double>();
    ServiceResult<PaymentDashboardDto>? after = null;
    for (var i = 0; i < runs; i++)
    {
        var (ms, result) = await TimeAsync(legacy: false);
        times.Add(ms);
        after = result;
    }
    times.Sort();
    var median = times[runs / 2];
    var p95 = times[(int)Math.Ceiling(0.95 * runs) - 1];
    var same = before.IsSuccess && after!.IsSuccess && DashboardTestData.Figures(before.Data!) == DashboardTestData.Figures(after.Data!);
    var fast = p95 < targetMs;
    failed |= !same || !fast;
    report.AppendLine($"| {s.Name} | {legacyMs:N0} ms | {median:N0} ms | {p95:N0} ms | {(same ? "yes" : "**no**")} | {(fast ? "yes" : "**no**")} |");
}

Console.WriteLine(report);
var summary = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
if (!string.IsNullOrEmpty(summary)) File.AppendAllText(summary, report.ToString());
return failed ? 1 : 0;
