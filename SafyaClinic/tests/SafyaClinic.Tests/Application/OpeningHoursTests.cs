using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Common.Auditing;
using SafyaClinic.Application.Scheduling;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Settings;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using SafyaClinic.Web.Models;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>P1: clinic opening hours: several periods a day, no overlaps, Saturday-first week, audited saves.</summary>
public class OpeningHoursTests
{
    private static TimeSpan At(int hour, int minute = 0) => new(hour, minute, 0);
    private static OpeningPeriodDto Period(DayOfWeek day, int from, int to) => new(day, At(from), At(to));

    // ── Rules ───────────────────────────────────────────────────

    [Fact]
    public void A_split_shift_is_valid()
    {
        var errors = OpeningHoursRules.Validate(new[]
        {
            Period(DayOfWeek.Saturday, 10, 14),
            Period(DayOfWeek.Saturday, 17, 21),
        });
        Assert.Empty(errors);
    }

    [Fact]
    public void Periods_may_touch_but_not_overlap()
    {
        Assert.Empty(OpeningHoursRules.Validate(new[] { Period(DayOfWeek.Sunday, 10, 14), Period(DayOfWeek.Sunday, 14, 18) }));
        Assert.Single(OpeningHoursRules.Validate(new[] { Period(DayOfWeek.Sunday, 10, 15), Period(DayOfWeek.Sunday, 14, 18) }));
    }

    [Fact]
    public void Same_times_on_different_days_are_fine()
    {
        Assert.Empty(OpeningHoursRules.Validate(new[] { Period(DayOfWeek.Monday, 10, 14), Period(DayOfWeek.Tuesday, 10, 14) }));
    }

    [Theory]
    [InlineData(14, 10)]
    [InlineData(10, 10)]
    public void A_period_must_close_after_it_opens(int from, int to)
    {
        Assert.Single(OpeningHoursRules.Validate(new[] { Period(DayOfWeek.Monday, from, to) }));
    }

    [Fact]
    public void A_period_shorter_than_15_minutes_is_refused()
    {
        Assert.Single(OpeningHoursRules.Validate(new[] { new OpeningPeriodDto(DayOfWeek.Monday, At(10), At(10, 10)) }));
    }

    [Fact]
    public void At_most_three_periods_a_day()
    {
        var four = new[] { Period(DayOfWeek.Friday, 8, 9), Period(DayOfWeek.Friday, 10, 11), Period(DayOfWeek.Friday, 12, 13), Period(DayOfWeek.Friday, 14, 15) };
        Assert.Contains(OpeningHoursRules.Validate(four), e => e.Contains("at most 3"));
    }

    [Fact]
    public void The_clinic_week_starts_on_Saturday()
    {
        Assert.Equal(DayOfWeek.Saturday, ClinicWeek.Days[0]);
        Assert.Equal(DayOfWeek.Friday, ClinicWeek.Days[6]);
        var sorted = OpeningHoursRules.Sorted(new[] { Period(DayOfWeek.Monday, 9, 12), Period(DayOfWeek.Saturday, 17, 21), Period(DayOfWeek.Saturday, 10, 14) });
        Assert.Equal(new[] { "10:00–14:00", "17:00–21:00", "09:00–12:00" }, sorted.Select(p => p.Period.ToString()));
    }

    // ── Form ────────────────────────────────────────────────────

    [Fact]
    public void Form_shows_every_day_with_three_period_rows()
    {
        var form = ClinicHoursForm.From(1, "Main", new[] { Period(DayOfWeek.Sunday, 10, 14) });

        Assert.Equal(7, form.Days.Count);
        Assert.All(form.Days, d => Assert.Equal(OpeningHoursRules.MaxPeriodsPerDay, d.Periods.Count));
        Assert.Equal(At(10), form.Days.Single(d => d.Day == DayOfWeek.Sunday).Periods[0].Opens);
    }

    [Fact]
    public void Form_ignores_empty_rows_and_flags_half_filled_ones()
    {
        var form = ClinicHoursForm.From(1, "Main", Array.Empty<OpeningPeriodDto>());
        form.Days[0].Periods[0] = new PeriodInput { Opens = At(10), Closes = At(14) };
        form.Days[1].Periods[0] = new PeriodInput { Opens = At(10) };

        var periods = form.ToPeriods(out var errors);

        Assert.Equal(new[] { Period(DayOfWeek.Saturday, 10, 14) }, periods);
        Assert.Contains(errors, e => e.StartsWith("Sunday, period 1"));
    }

    // ── Service ─────────────────────────────────────────────────

    private sealed class FixedContext : IAuditContext
    {
        public int? UserId => 1;
        public string? IpAddress => null;
        public string? RequestId => null;
    }

    private static async Task<(SafyaDbContext Db, int ClinicId)> ClinicAsync()
    {
        var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"hours-{Guid.NewGuid()}").Options);
        var clinic = new Clinic { Name = "Main" };
        db.Clinics.Add(clinic);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (db, clinic.Id);
    }

    private static ClinicHoursService Service(SafyaDbContext db)
    {
        db.ChangeTracker.Clear();   // each call is a new request in the app
        var uow = new UnitOfWork(db);
        return new ClinicHoursService(uow, new AuditTrail(uow, new FixedContext()));
    }

    [Fact]
    public async Task Saving_replaces_the_whole_week_and_reads_back_Saturday_first()
    {
        var (db, id) = await ClinicAsync();
        await Service(db).SaveAsync(id, new[] { Period(DayOfWeek.Monday, 9, 12) }, 1);

        var saved = await Service(db).SaveAsync(id, new[] { Period(DayOfWeek.Sunday, 17, 21), Period(DayOfWeek.Saturday, 10, 14) }, 1);
        var week = (await Service(db).GetAsync(id)).Data!;

        Assert.True(saved.IsSuccess);
        Assert.Equal(new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }, week.Select(p => p.Day));
        Assert.Equal(2, await db.ClinicOpeningHours.CountAsync());
    }

    [Fact]
    public async Task Invalid_hours_change_nothing()
    {
        var (db, id) = await ClinicAsync();
        await Service(db).SaveAsync(id, new[] { Period(DayOfWeek.Monday, 9, 12) }, 1);

        var result = await Service(db).SaveAsync(id, new[] { Period(DayOfWeek.Monday, 12, 9) }, 1);

        Assert.False(result.IsSuccess);
        Assert.Equal(new[] { Period(DayOfWeek.Monday, 9, 12) }, (await Service(db).GetAsync(id)).Data!);
    }

    [Fact]
    public async Task Changes_are_audited_and_unchanged_saves_are_not()
    {
        var (db, id) = await ClinicAsync();
        await Service(db).SaveAsync(id, new[] { Period(DayOfWeek.Monday, 9, 12) }, 1);
        await Service(db).SaveAsync(id, new[] { Period(DayOfWeek.Monday, 9, 12) }, 1);

        var entries = await db.AuditLogs.Where(a => a.Action == AuditActions.ClinicHoursChanged).ToListAsync();
        var entry = Assert.Single(entries);
        Assert.Equal(id, entry.EntityId);
    }

    [Fact]
    public async Task Unknown_clinic_is_refused()
    {
        var (db, _) = await ClinicAsync();

        Assert.False((await Service(db).GetAsync(987654)).IsSuccess);
        Assert.False((await Service(db).SaveAsync(987654, Array.Empty<OpeningPeriodDto>(), 1)).IsSuccess);
    }

    [Fact]
    public async Task A_clinic_with_no_hours_has_an_empty_week()
    {
        var (db, id) = await ClinicAsync();

        Assert.Empty((await Service(db).GetAsync(id)).Data!);
    }
}
