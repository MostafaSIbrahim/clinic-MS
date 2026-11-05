using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SafyaClinic.Application.Common.Auditing;
using SafyaClinic.Application.Scheduling;
using SafyaClinic.Application.Security;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Settings;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using SafyaClinic.Tests.Web.Security;
using SafyaClinic.Web.Controllers;
using SafyaClinic.Web.Models;
using SafyaClinic.Web.Security;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>P1b: doctors' calendars: modes, steps, breaks, slots, clinic hours, no overlap across clinics, who may edit.</summary>
public class DoctorScheduleTests
{
    private static TimeSpan At(int hour, int minute = 0) => new(hour, minute, 0);
    private static ScheduleTimeDto Time(DayOfWeek day, ScheduleTimeKind kind, int from, int to) => new(day, kind, At(from), At(to));
    private static ScheduleTimeDto Working(int from, int to, DayOfWeek day = DayOfWeek.Saturday) => Time(day, ScheduleTimeKind.Working, from, to);
    private static ScheduleTimeDto Break(int from, int to, DayOfWeek day = DayOfWeek.Saturday) => Time(day, ScheduleTimeKind.Break, from, to);
    private static ScheduleTimeDto Slot(int from, int to, DayOfWeek day = DayOfWeek.Saturday) => Time(day, ScheduleTimeKind.Slot, from, to);

    /// <summary>Saturday 10–14 and 17–21; Sunday 10–14; closed otherwise.</summary>
    private static readonly OpeningPeriodDto[] ClinicHours =
    {
        new(DayOfWeek.Saturday, At(10), At(14)), new(DayOfWeek.Saturday, At(17), At(21)), new(DayOfWeek.Sunday, At(10), At(14)),
    };

    private static DoctorScheduleDto Open(params ScheduleTimeDto[] times) => new(1, 1, ScheduleMode.Open, 15, true, times);
    private static DoctorScheduleDto Slots(params ScheduleTimeDto[] times) => new(1, 1, ScheduleMode.SpecificSlots, 15, true, times);
    private static IReadOnlyList<string> Check(DoctorScheduleDto s, params OtherClinicTime[] others) => DoctorScheduleRules.Validate(s, ClinicHours, others);

    // ── Rules ───────────────────────────────────────────────────

    [Fact]
    public void An_open_calendar_with_a_split_shift_and_a_break_is_valid()
    {
        Assert.Empty(Check(Open(Working(10, 14), Break(12, 13), Working(17, 21))));
    }

    [Fact]
    public void Working_periods_must_be_inside_the_clinic_hours()
    {
        Assert.Contains(Check(Open(Working(9, 13))), e => e.Contains("outside the clinic's opening hours"));
        Assert.Contains(Check(Open(Working(12, 18))), e => e.Contains("outside the clinic's opening hours"));
    }

    [Fact]
    public void Nothing_can_be_set_on_a_day_the_clinic_is_closed()
    {
        Assert.Contains(Check(Open(Working(10, 12, DayOfWeek.Monday))), e => e.Contains("closed"));
    }

    [Fact]
    public void A_break_must_be_inside_a_working_period()
    {
        Assert.Contains(Check(Open(Working(10, 12), Break(13, 14))), e => e.Contains("inside a working period"));
    }

    [Fact]
    public void Overlapping_working_periods_are_refused()
    {
        Assert.Contains(Check(Open(Working(10, 13), Working(12, 14))), e => e.Contains("overlap"));
    }

    [Fact]
    public void Specific_slots_inside_clinic_hours_are_valid_and_at_least_one_step_long()
    {
        Assert.Empty(Check(Slots(Slot(10, 11), Slot(17, 19))));
        var tooShort = Slots(new ScheduleTimeDto(DayOfWeek.Saturday, ScheduleTimeKind.Slot, At(10), At(10, 10)));
        Assert.Contains(Check(tooShort), e => e.Contains("shorter than the 15-minute step"));
    }

    [Fact]
    public void Each_mode_only_accepts_its_own_kind_of_period()
    {
        Assert.Contains(Check(Open(Working(10, 14), Slot(10, 11))), e => e.Contains("don't belong"));
        Assert.Contains(Check(Slots(Slot(10, 11), Working(10, 14))), e => e.Contains("don't belong"));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(45)]
    public void Only_the_offered_steps_are_accepted(int step)
    {
        Assert.Contains(Check(Open(Working(10, 14)) with { StepMinutes = step }), e => e.Contains("step"));
    }

    [Fact]
    public void An_active_calendar_needs_some_availability_but_an_unused_one_does_not()
    {
        Assert.Contains(Check(Open()), e => e.Contains("at least one working period"));
        Assert.Empty(Check(Open() with { IsActive = false }));
    }

    [Fact]
    public void A_doctor_cannot_be_in_two_clinics_at_the_same_time_but_may_move_between_them()
    {
        var elsewhere = new OtherClinicTime("Nasr City", Working(13, 16));
        Assert.Contains(Check(Open(Working(10, 14)), elsewhere), e => e.Contains("Nasr City"));

        var afterwards = new OtherClinicTime("Nasr City", Working(14, 16));
        Assert.Empty(Check(Open(Working(10, 14)), afterwards));
    }

    // ── Form ────────────────────────────────────────────────────

    [Fact]
    public void Form_saves_only_the_chosen_modes_rows()
    {
        var view = new DoctorScheduleView("Dr Karim", "Main", false, Open(), ClinicHours);
        var form = DoctorScheduleForm.From(view);
        form.Days[0].Working[0] = new PeriodInput { Opens = At(10), Closes = At(14) };
        form.Days[0].Slots[0] = new PeriodInput { Opens = At(10), Closes = At(11) };

        var open = form.ToSchedule(out var errors);
        Assert.Empty(errors);
        Assert.Equal(new[] { Working(10, 14) }, open.Times);

        form.Mode = ScheduleMode.SpecificSlots;
        Assert.Equal(new[] { Slot(10, 11) }, form.ToSchedule(out _).Times);
    }

    [Fact]
    public void Form_shows_clinic_hours_per_day_and_flags_half_filled_rows()
    {
        var form = DoctorScheduleForm.From(new DoctorScheduleView("Dr Karim", "Main", false, Open(), ClinicHours));
        Assert.Equal(new[] { "10:00–14:00", "17:00–21:00" }, form.Days[0].ClinicHours);
        Assert.Empty(form.Days[2].ClinicHours);

        form.Days[0].Breaks[0] = new PeriodInput { Opens = At(12) };
        form.ToSchedule(out var errors);
        Assert.Contains(errors, e => e.StartsWith("Saturday, break 1"));
    }

    // ── Service ─────────────────────────────────────────────────

    private sealed class FixedContext : IAuditContext
    {
        public int? UserId => 1;
        public string? IpAddress => null;
        public string? RequestId => null;
    }

    private sealed record World(SafyaDbContext Db, int DoctorId, int ReceptionId, int MainId, int BranchId);

    private static async Task<World> WorldAsync()
    {
        var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"schedules-{Guid.NewGuid()}").Options);
        var doctor = new User { FullName = "Dr Karim", PhoneNumber = "0101", PasswordHash = "x", ClinicianType = ClinicianType.Doctor };
        var reception = new User { FullName = "Front desk", PhoneNumber = "0102", PasswordHash = "x" };
        var main = new Clinic { Name = "Main" };
        var branch = new Clinic { Name = "Nasr City" };
        db.AddRange(doctor, reception, main, branch);
        await db.SaveChangesAsync();
        foreach (var clinic in new[] { main, branch })
            db.ClinicOpeningHours.Add(new ClinicOpeningHours { ClinicId = clinic.Id, DayOfWeek = DayOfWeek.Saturday, OpensAt = At(9), ClosesAt = At(21) });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new World(db, doctor.Id, reception.Id, main.Id, branch.Id);
    }

    private static DoctorScheduleService Service(SafyaDbContext db)
    {
        db.ChangeTracker.Clear();   // each call is a new request in the app
        var uow = new UnitOfWork(db);
        return new DoctorScheduleService(uow, new AuditTrail(uow, new FixedContext()));
    }

    private static DoctorScheduleDto For(World w, int clinicId, params ScheduleTimeDto[] times) =>
        new(w.DoctorId, clinicId, ScheduleMode.Open, 15, true, times);

    [Fact]
    public async Task A_doctor_without_a_calendar_gets_an_open_15_minute_one_to_start_from()
    {
        var w = await WorldAsync();

        var view = (await Service(w.Db).GetAsync(w.DoctorId, w.MainId)).Data!;

        Assert.False(view.Exists);
        Assert.Equal(ScheduleMode.Open, view.Schedule.Mode);
        Assert.Equal(15, view.Schedule.StepMinutes);
        Assert.Single(view.ClinicHours);
    }

    [Fact]
    public async Task Saving_creates_then_replaces_the_calendar_and_audits_each_change()
    {
        var w = await WorldAsync();
        Assert.True((await Service(w.Db).SaveAsync(For(w, w.MainId, Working(10, 14)), 1)).IsSuccess);
        Assert.True((await Service(w.Db).SaveAsync(For(w, w.MainId, Working(15, 19), Break(16, 17)), 1)).IsSuccess);
        Assert.True((await Service(w.Db).SaveAsync(For(w, w.MainId, Working(15, 19), Break(16, 17)), 1)).IsSuccess);   // unchanged

        var saved = (await Service(w.Db).GetAsync(w.DoctorId, w.MainId)).Data!.Schedule;
        Assert.Equal(new[] { Working(15, 19), Break(16, 17) }, saved.Times);
        Assert.Equal(1, await w.Db.DoctorSchedules.CountAsync());
        Assert.Equal(2, await w.Db.AuditLogs.CountAsync(a => a.Action == AuditActions.ScheduleChanged));
    }

    [Fact]
    public async Task Overlap_with_the_doctors_other_clinic_is_refused_unless_that_calendar_is_not_in_use()
    {
        var w = await WorldAsync();
        await Service(w.Db).SaveAsync(For(w, w.BranchId, Working(12, 16)), 1);

        var clash = await Service(w.Db).SaveAsync(For(w, w.MainId, Working(10, 14)), 1);
        Assert.False(clash.IsSuccess);
        Assert.Contains(clash.Errors, e => e.Contains("Nasr City"));

        await Service(w.Db).SaveAsync(For(w, w.BranchId, Working(12, 16)) with { IsActive = false }, 1);
        Assert.True((await Service(w.Db).SaveAsync(For(w, w.MainId, Working(10, 14)), 1)).IsSuccess);
    }

    [Fact]
    public async Task Only_clinicians_have_a_calendar()
    {
        var w = await WorldAsync();

        Assert.False((await Service(w.Db).GetAsync(w.ReceptionId, w.MainId)).IsSuccess);
        var list = (await Service(w.Db).ListAsync()).Data!;
        Assert.Equal(2, list.Count);   // one doctor in two clinics
        Assert.All(list, row => Assert.Equal(w.DoctorId, row.DoctorId));
        Assert.Empty((await Service(w.Db).ListAsync(w.ReceptionId)).Data!);
    }

    [Fact]
    public async Task Invalid_calendars_change_nothing()
    {
        var w = await WorldAsync();
        await Service(w.Db).SaveAsync(For(w, w.MainId, Working(10, 14)), 1);

        Assert.False((await Service(w.Db).SaveAsync(For(w, w.MainId, Working(6, 8)), 1)).IsSuccess);
        Assert.Equal(new[] { Working(10, 14) }, (await Service(w.Db).GetAsync(w.DoctorId, w.MainId)).Data!.Schedule.Times);
    }

    // ── Who may edit ────────────────────────────────────────────

    private sealed class StubSchedules : SafyaClinic.Application.Interfaces.Services.IDoctorScheduleService
    {
        public Task<SafyaClinic.Application.DTOs.Common.ServiceResult<IReadOnlyList<ScheduleSummaryDto>>> ListAsync(int? doctorId = null) =>
            Task.FromResult(SafyaClinic.Application.DTOs.Common.ServiceResult<IReadOnlyList<ScheduleSummaryDto>>.Success(Array.Empty<ScheduleSummaryDto>()));

        public Task<SafyaClinic.Application.DTOs.Common.ServiceResult<DoctorScheduleView>> GetAsync(int doctorId, int clinicId) =>
            Task.FromResult(SafyaClinic.Application.DTOs.Common.ServiceResult<DoctorScheduleView>.Success(
                new DoctorScheduleView("Dr", "Main", false, new DoctorScheduleDto(doctorId, clinicId, ScheduleMode.Open, 15, true, Array.Empty<ScheduleTimeDto>()), ClinicHours)));

        public Task<SafyaClinic.Application.DTOs.Common.ServiceResult> SaveAsync(DoctorScheduleDto schedule, int actingUserId) =>
            Task.FromResult(SafyaClinic.Application.DTOs.Common.ServiceResult.Success());

        public Task<SafyaClinic.Application.DTOs.Common.ServiceResult> SetDateChangeAsync(int doctorId, int clinicId, DateChangeDto change, int actingUserId) =>
            Task.FromResult(SafyaClinic.Application.DTOs.Common.ServiceResult.Success());

        public Task<SafyaClinic.Application.DTOs.Common.ServiceResult> RemoveDateChangeAsync(int doctorId, int clinicId, DateTime date, int actingUserId) =>
            Task.FromResult(SafyaClinic.Application.DTOs.Common.ServiceResult.Success());

        public Task<SafyaClinic.Application.DTOs.Common.ServiceResult<IReadOnlyList<TimeSpan>>> FreeStartsAsync(int doctorId, int clinicId, DateTime date, int visitMinutes) =>
            Task.FromResult(SafyaClinic.Application.DTOs.Common.ServiceResult<IReadOnlyList<TimeSpan>>.Success(Array.Empty<TimeSpan>()));
    }

    private sealed class NoTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static SchedulesController Controller(string role, int userId)
    {
        var services = new ServiceCollection().AddSingleton<ICurrentPermissions>(new RolePermissions(new[] { role })).BuildServiceProvider();
        var http = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test")),
        };
        return new SchedulesController(new StubSchedules())
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NoTempDataProvider()),
        };
    }

    [Fact]
    public async Task A_doctor_may_open_their_own_calendar_but_not_another_doctors()
    {
        Assert.IsType<ViewResult>(await Controller(SystemGroups.DoctorRole, userId: 7).Edit(doctorId: 7, clinicId: 1));
        Assert.IsType<ForbidResult>(await Controller(SystemGroups.DoctorRole, userId: 7).Edit(doctorId: 8, clinicId: 1));
    }

    [Fact]
    public async Task Reception_supervisors_may_open_any_calendar_and_reception_none()
    {
        Assert.IsType<ViewResult>(await Controller(SystemGroups.ReceptionAdminRole, userId: 3).Edit(doctorId: 8, clinicId: 1));
        Assert.IsType<ForbidResult>(await Controller(SystemGroups.ReceptionRole, userId: 3).Edit(doctorId: 8, clinicId: 1));
    }
}
