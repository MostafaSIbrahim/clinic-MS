using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.DTOs.Nutrition;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>
/// M4d: the patient's plan for the coming weeks pre-fills each week's form (ticked), and items outside the
/// package (catalogue injections or vitamins, or a typed drug) can be given and planned. The package never changes.
/// </summary>
public class PlanAndExtraItemsTests
{
    private sealed record World(SafyaDbContext Db, int Enrollment, int Week1Item, int Week2Item, int Injection, int Vitamin, int Doctor, int Package);

    private static async Task<World> WorldAsync()
    {
        var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"plan-{Guid.NewGuid()}").Options);
        var doctor = new User { FullName = "Dr Salma", PhoneNumber = "0402", PasswordHash = "x", ClinicianType = ClinicianType.Doctor };
        var patient = new Patient { FirstName = "Mona", LastName = "Adel" };
        var package = new NutritionPackage { PackageName = "Monthly", BasePrice = 1000m };
        var injection = new InjectionType { InjectionName = "Lipo C", Unit = "ml" };
        var vitamin = new VitaminType { VitaminName = "Zinc", Unit = "tab" };
        db.AddRange(doctor, patient, package, injection, vitamin);
        await db.SaveChangesAsync();
        var week1 = new PackageItem { PackageId = package.Id, WeekNumber = 1, Quantity = 1m, Unit = "ml", InjectionId = injection.Id };
        var week2 = new PackageItem { PackageId = package.Id, WeekNumber = 2, Quantity = 2m, Unit = "tab", VitaminId = vitamin.Id };
        var enrollment = new PatientNutritionEnrollment
        {
            PatientId = patient.Id, PackageId = package.Id, DoctorId = doctor.Id, BasePrice = 1000m, Status = EnrollmentStatus.Active,
            StartDate = new DateTime(2026, 11, 1), EndDate = new DateTime(2026, 11, 29),
        };
        db.AddRange(week1, week2, enrollment);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new World(db, enrollment.Id, week1.Id, week2.Id, injection.Id, vitamin.Id, doctor.Id, package.Id);
    }

    private static NutritionService Service(World w)
    {
        w.Db.ChangeTracker.Clear();
        return new NutritionService(new UnitOfWork(w.Db));
    }

    private static RecordFollowUpDto Form() => new() { FollowUpDate = new DateTime(2026, 11, 8), WeightKg = 80m };

    [Fact]
    public async Task Without_a_plan_the_form_starts_from_the_package_with_this_weeks_items_ticked()
    {
        var w = await WorldAsync();

        var form = (await Service(w).GetFollowUpFormAsync(w.Enrollment, null)).Data!;

        Assert.Equal(1, form.Week);
        var thisWeek = Assert.Single(form.Lines, l => l.Given);
        Assert.Equal(w.Week1Item, thisWeek.PackageItemId);
        Assert.Contains(form.Lines, l => l.PackageItemId == w.Week2Item && !l.Given && l.OtherWeek);
        Assert.Equal(new[] { 2, 3, 4 }, form.PlanWeeks);
        Assert.Equal(w.Week2Item, Assert.Single(form.Plan).PackageItemId);   // the package's week 2
    }

    [Fact]
    public async Task The_plan_saved_with_week_1_prefills_week_2_ticked_including_items_outside_the_package()
    {
        var w = await WorldAsync();
        var week1 = Form();
        week1.AdministeredItems.Add(new RecordAdministeredItemDto { Given = true, PackageItemId = w.Week1Item, ActualQuantity = 1m });
        week1.AdministeredItems.Add(new RecordAdministeredItemDto { Given = true, CustomName = " Metformin 500 ", ActualQuantity = 1m, Unit = "tab" });
        week1.PlannedItems.Add(new RecordPlannedItemDto { WeekNumber = 2, PackageItemId = w.Week2Item, Quantity = 3m });   // changed from 2
        week1.PlannedItems.Add(new RecordPlannedItemDto { WeekNumber = 2, InjectionId = w.Injection, Quantity = 1m });     // outside the package
        week1.PlannedItems.Add(new RecordPlannedItemDto { WeekNumber = 3, CustomName = "Omega 3", Quantity = 2m, Unit = "cap" });

        var recorded = await Service(w).RecordFollowUpAsync(w.Enrollment, week1, w.Doctor);
        Assert.True(recorded.IsSuccess, string.Join(" ", recorded.Errors));

        var given = await w.Db.WeeklyAdministeredItems.AsNoTracking().OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(2, given.Count);
        Assert.Equal("Metformin 500", given[1].CustomName);
        Assert.Null(given[1].PackageItemId);
        var shown = (await Service(w).GetFollowUpByIdAsync(recorded.Data!.Id)).Data!;
        Assert.Contains(shown.AdministeredItems, a => a.ItemName == "Metformin 500" && a.OutsidePackage);

        var form = (await Service(w).GetFollowUpFormAsync(w.Enrollment, null)).Data!;
        Assert.Equal(2, form.Week);
        var ticked = form.Lines.Where(l => l.Given).ToList();
        Assert.Equal(2, ticked.Count);
        Assert.Contains(ticked, l => l.PackageItemId == w.Week2Item && l.Quantity == 3m);
        Assert.Contains(ticked, l => l.InjectionId == w.Injection && l.OutsidePackage && l.Name == "Lipo C");
        Assert.Equal(new[] { 3, 4 }, form.PlanWeeks);
        Assert.Equal("Omega 3", Assert.Single(form.Plan).CustomName);   // week 3 as planned; week 4 left empty

        var enrollment = (await Service(w).GetEnrollmentByIdAsync(w.Enrollment)).Data!;
        Assert.Equal(3, enrollment.PlannedItems.Count);                 // weeks 2 and 3, shown on the enrollment page

        var packageItems = await w.Db.PackageItems.AsNoTracking().Where(p => p.PackageId == w.Package).ToListAsync();
        Assert.Equal(2, packageItems.Count);                            // the package never changes
        Assert.Equal(2m, packageItems.Single(p => p.Id == w.Week2Item).Quantity);
    }

    [Fact]
    public async Task Editing_a_week_replaces_the_plan_for_the_weeks_still_to_come()
    {
        var w = await WorldAsync();
        var first = Form();
        first.PlannedItems.Add(new RecordPlannedItemDto { WeekNumber = 2, VitaminId = w.Vitamin, Quantity = 1m });
        var id = (await Service(w).RecordFollowUpAsync(w.Enrollment, first, w.Doctor)).Data!.Id;

        var edit = Form();
        edit.PlannedItems.Add(new RecordPlannedItemDto { WeekNumber = 3, CustomName = "Iron", Quantity = 1m });
        Assert.True((await Service(w).UpdateFollowUpAsync(id, edit, w.Doctor)).IsSuccess);

        var plan = await w.Db.EnrollmentPlannedItems.AsNoTracking().ToListAsync();
        Assert.Equal("Iron", Assert.Single(plan).CustomName);
    }

    public static TheoryData<RecordPlannedItemDto> BadPlans => new()
    {
        new RecordPlannedItemDto { WeekNumber = 2, Quantity = 1m },                                          // names nothing
        new RecordPlannedItemDto { WeekNumber = 2, InjectionId = 1, VitaminId = 1, Quantity = 1m },          // names two
        new RecordPlannedItemDto { WeekNumber = 2, CustomName = "Iron", Quantity = 0m },                     // no quantity
        new RecordPlannedItemDto { WeekNumber = 1, CustomName = "Iron", Quantity = 1m },                     // this week, not a coming one
        new RecordPlannedItemDto { WeekNumber = 5, CustomName = "Iron", Quantity = 1m },                     // past week 4
        new RecordPlannedItemDto { WeekNumber = 2, InjectionId = 999, Quantity = 1m },                       // unknown injection
        new RecordPlannedItemDto { WeekNumber = 2, CustomName = new string('x', 201), Quantity = 1m },       // name too long
    };

    [Theory]
    [MemberData(nameof(BadPlans))]
    public async Task An_invalid_planned_item_is_refused_and_nothing_is_saved(RecordPlannedItemDto bad)
    {
        var w = await WorldAsync();
        var form = Form();
        form.PlannedItems.Add(bad);

        var result = await Service(w).RecordFollowUpAsync(w.Enrollment, form, w.Doctor);

        Assert.False(result.IsSuccess);
        Assert.Empty(await w.Db.WeeklyFollowUps.ToListAsync());
        Assert.Empty(await w.Db.EnrollmentPlannedItems.ToListAsync());
    }

    [Fact]
    public async Task A_given_item_outside_the_package_must_name_exactly_one_item()
    {
        var w = await WorldAsync();
        var form = Form();
        form.AdministeredItems.Add(new RecordAdministeredItemDto { Given = true, InjectionId = w.Injection, CustomName = "Also this", ActualQuantity = 1m });

        Assert.False((await Service(w).RecordFollowUpAsync(w.Enrollment, form, w.Doctor)).IsSuccess);
    }

    [Fact]
    public async Task Unticked_lines_are_not_recorded_as_given()
    {
        var w = await WorldAsync();
        var form = Form();
        form.AdministeredItems.Add(new RecordAdministeredItemDto { Given = false, PackageItemId = w.Week1Item, ActualQuantity = 1m });

        Assert.True((await Service(w).RecordFollowUpAsync(w.Enrollment, form, w.Doctor)).IsSuccess);
        Assert.Empty(await w.Db.WeeklyAdministeredItems.ToListAsync());
    }
}
