using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Common.Auditing;
using SafyaClinic.Application.DTOs.Nutrition;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Analysis;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>
/// M4b: weekly follow-ups. Edit changes the same week, items come from the enrollment's package, lab results name
/// a known analysis, and only the latest week can be deleted (deleting week 4 reopens a completed enrollment).
/// </summary>
public class FollowUpTests
{
    private sealed class Recorder : IAuditTrail
    {
        public List<string> Actions { get; } = new();
        public Task RecordAsync(string action, string entityType, int? entityId, object? details = null, int? actingUserId = null)
        {
            Actions.Add(action);
            return Task.CompletedTask;
        }
    }

    private sealed record World(SafyaDbContext Db, int Enrollment, int Week1Item, int Week2Item, int OtherPackageItem, int Analysis, int Doctor);

    private static async Task<World> WorldAsync(EnrollmentStatus status = EnrollmentStatus.Active)
    {
        var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"followup-{Guid.NewGuid()}").Options);
        var doctor = new User { FullName = "Dr Salma", PhoneNumber = "0402", PasswordHash = "x", ClinicianType = ClinicianType.Doctor };
        var patient = new Patient { FirstName = "Mona", LastName = "Adel" };
        var package = new NutritionPackage { PackageName = "Monthly", BasePrice = 1000m };
        var other = new NutritionPackage { PackageName = "Other", BasePrice = 500m };
        var analysis = new AnalysisType { TypeName = "HbA1c" };
        db.AddRange(doctor, patient, package, other, analysis);
        await db.SaveChangesAsync();
        var week1 = new PackageItem { PackageId = package.Id, WeekNumber = 1, Quantity = 1m, Unit = "ml" };
        var week2 = new PackageItem { PackageId = package.Id, WeekNumber = 2, Quantity = 2m, Unit = "tab" };
        var foreign = new PackageItem { PackageId = other.Id, WeekNumber = 1, Quantity = 1m };
        var enrollment = new PatientNutritionEnrollment
        {
            PatientId = patient.Id, PackageId = package.Id, DoctorId = doctor.Id, BasePrice = 1000m, Status = status,
            StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 29),
        };
        db.AddRange(week1, week2, foreign, enrollment);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new World(db, enrollment.Id, week1.Id, week2.Id, foreign.Id, analysis.Id, doctor.Id);
    }

    private static NutritionService Service(World w, IAuditTrail? audit = null)
    {
        w.Db.ChangeTracker.Clear();
        return new NutritionService(new UnitOfWork(w.Db), audit ?? new Recorder());
    }

    private static RecordFollowUpDto Form(decimal weight, params RecordAdministeredItemDto[] items) => new()
    {
        FollowUpId = 0,
        FollowUpDate = new DateTime(2026, 11, 8),
        WeightKg = weight,
        HeightCm = 165m,
        AdministeredItems = items.ToList(),
    };

    private static async Task<int> RecordAsync(World w, decimal weight = 80m)
    {
        var result = await Service(w).RecordFollowUpAsync(w.Enrollment, Form(weight), w.Doctor);
        Assert.True(result.IsSuccess, string.Join(" ", result.Errors));
        return result.Data!.Id;
    }

    private Task<List<WeeklyFollowUp>> WeeksAsync(World w) =>
        w.Db.WeeklyFollowUps.AsNoTracking().Where(f => f.EnrollmentId == w.Enrollment).OrderBy(f => f.WeekNumber).ToListAsync();

    [Fact]
    public async Task Editing_changes_the_same_week_and_replaces_its_items_and_lab_results()
    {
        var w = await WorldAsync();
        var id = (await Service(w).RecordFollowUpAsync(w.Enrollment,
            Form(80m, new RecordAdministeredItemDto { Given = true, PackageItemId = w.Week1Item, ActualQuantity = 1m }), w.Doctor)).Data!.Id;

        var edit = Form(78.5m, new RecordAdministeredItemDto { Given = true, PackageItemId = w.Week2Item, ActualQuantity = 2m, Notes = "late" });
        edit.LabResults.Add(new RecordLabResultDto { AnalysisTypeId = w.Analysis, ResultValue = "5.6", IsNormal = true });
        var audit = new Recorder();
        var result = await Service(w, audit).UpdateFollowUpAsync(id, edit, w.Doctor);

        Assert.True(result.IsSuccess, string.Join(" ", result.Errors));
        var weeks = await WeeksAsync(w);
        var only = Assert.Single(weeks);                    // no new week was added (N1)
        Assert.Equal(1, only.WeekNumber);
        Assert.Equal(78.5m, only.WeightKg);
        Assert.Equal(165m, only.HeightCm);
        var item = Assert.Single(await w.Db.WeeklyAdministeredItems.AsNoTracking().Where(a => a.FollowUpId == id).ToListAsync());
        Assert.Equal(w.Week2Item, item.PackageItemId);
        Assert.Equal("late", item.Notes);
        Assert.Single(await w.Db.WeeklyFollowUpLabResults.AsNoTracking().Where(l => l.FollowUpId == id).ToListAsync());
        Assert.Contains(AuditActions.FollowUpEdited, audit.Actions);

        var shown = (await Service(w).GetFollowUpByIdAsync(id)).Data!;   // what the edit form is filled from
        Assert.Equal(w.Week2Item, Assert.Single(shown.AdministeredItems).PackageItemId);
        Assert.Equal(w.Analysis, Assert.Single(shown.LabResults).AnalysisTypeId);
        Assert.Equal(165m, shown.HeightCm);
    }

    [Fact]
    public async Task An_item_from_another_package_is_refused_not_silently_dropped()
    {
        var w = await WorldAsync();

        var result = await Service(w).RecordFollowUpAsync(w.Enrollment,
            Form(80m, new RecordAdministeredItemDto { Given = true, PackageItemId = w.OtherPackageItem, ActualQuantity = 1m }), w.Doctor);

        Assert.False(result.IsSuccess);
        Assert.Empty(await WeeksAsync(w));
    }

    [Theory]
    [InlineData(999, "5.6", "Poor")]       // unknown analysis
    [InlineData(null, "5.6", "Poor")]      // a result without its analysis
    [InlineData(0, null, "Superb")]        // not a diet compliance value (0 = use the known analysis)
    public async Task Invalid_lab_results_or_diet_compliance_are_refused(int? analysis, string? value, string diet)
    {
        var w = await WorldAsync();
        var form = Form(80m);
        form.DietCompliance = diet;
        form.LabResults.Add(new RecordLabResultDto { AnalysisTypeId = analysis == 0 ? w.Analysis : analysis, ResultValue = value });

        var result = await Service(w).RecordFollowUpAsync(w.Enrollment, form, w.Doctor);

        Assert.False(result.IsSuccess);
        Assert.Empty(await WeeksAsync(w));
    }

    [Fact]
    public async Task A_ticked_item_needs_a_quantity()
    {
        var w = await WorldAsync();

        var result = await Service(w).RecordFollowUpAsync(w.Enrollment,
            Form(80m, new RecordAdministeredItemDto { Given = true, PackageItemId = w.Week1Item, ActualQuantity = null }), w.Doctor);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Only_the_latest_week_can_be_deleted()
    {
        var w = await WorldAsync();
        var week1 = await RecordAsync(w);
        var week2 = await RecordAsync(w);

        var first = await Service(w).DeleteFollowUpAsync(week1);
        Assert.False(first.IsSuccess);
        Assert.Contains(NutritionService.OnlyLatestWeekMessage, first.Errors);

        Assert.True((await Service(w).DeleteFollowUpAsync(week2)).IsSuccess);
        Assert.Equal(new[] { 1 }, (await WeeksAsync(w)).Select(f => f.WeekNumber));

        await RecordAsync(w);                                      // the next one is week 2 again: no gaps
        Assert.Equal(new[] { 1, 2 }, (await WeeksAsync(w)).Select(f => f.WeekNumber));
    }

    [Fact]
    public async Task Deleting_week_4_of_a_completed_enrollment_makes_it_active_again()
    {
        var w = await WorldAsync();
        int last = 0;
        for (var i = 0; i < 4; i++) last = await RecordAsync(w);
        Assert.Equal(EnrollmentStatus.Completed,
            await w.Db.PatientNutritionEnrollments.AsNoTracking().Where(e => e.Id == w.Enrollment).Select(e => e.Status).SingleAsync());

        var audit = new Recorder();
        var result = await Service(w, audit).DeleteFollowUpAsync(last);

        Assert.True(result.IsSuccess, string.Join(" ", result.Errors));
        Assert.Equal(EnrollmentStatus.Active,
            await w.Db.PatientNutritionEnrollments.AsNoTracking().Where(e => e.Id == w.Enrollment).Select(e => e.Status).SingleAsync());
        Assert.Contains(AuditActions.EnrollmentStatusChanged, audit.Actions);
        Assert.Contains(AuditActions.FollowUpDeleted, audit.Actions);
        Assert.Equal(3, (await WeeksAsync(w)).Count);
    }

    [Fact]
    public async Task Follow_ups_of_a_cancelled_enrollment_cannot_be_changed_or_deleted()
    {
        var w = await WorldAsync();
        var id = await RecordAsync(w);
        var enrollment = await w.Db.PatientNutritionEnrollments.SingleAsync(e => e.Id == w.Enrollment);
        enrollment.Status = EnrollmentStatus.Cancelled;
        await w.Db.SaveChangesAsync();

        Assert.False((await Service(w).UpdateFollowUpAsync(id, Form(70m), w.Doctor)).IsSuccess);
        Assert.False((await Service(w).DeleteFollowUpAsync(id)).IsSuccess);
        Assert.Equal(80m, Assert.Single(await WeeksAsync(w)).WeightKg);
    }
}
