using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Catalogue;
using SafyaClinic.Domain.Entities.Analysis;
using SafyaClinic.Domain.Entities.MedicalRecord;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Entities.Reservation;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>
/// M5a: the one catalogue editor. An item already used is made inactive instead of deleted (F33, now also for
/// analysis types and for items given or planned outside a package, M4d); forms are checked per catalogue.
/// </summary>
public class CatalogueServiceTests
{
    private static SafyaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<SafyaDbContext>().UseInMemoryDatabase($"catalogue-{Guid.NewGuid()}").Options);

    private static CatalogueService Service(SafyaDbContext db)
    {
        db.ChangeTracker.Clear();
        return new CatalogueService(new UnitOfWork(db));
    }

    // ── Delete: used items are made inactive, unused ones deleted ──

    [Fact]
    public async Task Treatment_type_used_by_a_reservation_is_deactivated_not_deleted()
    {
        await using var db = NewContext();
        var type = new TreatmentType { TypeName = "Consultation" };
        db.TreatmentTypes.Add(type);
        await db.SaveChangesAsync();
        db.Reservations.Add(new Reservation { TreatmentTypeId = type.Id, PatientId = 1, DoctorId = 1, ClinicId = 1, StatusId = 1 });
        await db.SaveChangesAsync();

        var result = await Service(db).DeleteAsync(CatalogueKind.Treatment, type.Id);

        Assert.Equal(CatalogueService.Deactivated, result.Message);
        Assert.False((await db.TreatmentTypes.AsNoTracking().SingleAsync()).IsActive);
    }

    [Fact]
    public async Task Unused_treatment_type_is_deleted()
    {
        await using var db = NewContext();
        var type = new TreatmentType { TypeName = "Unused" };
        db.TreatmentTypes.Add(type);
        await db.SaveChangesAsync();

        var result = await Service(db).DeleteAsync(CatalogueKind.Treatment, type.Id);

        Assert.Equal(CatalogueService.Deleted, result.Message);
        Assert.False(await db.TreatmentTypes.AnyAsync());
    }

    [Theory]
    [InlineData("package")]
    [InlineData("given")]      // M4d: given outside the package
    [InlineData("planned")]    // M4d: in a patient's plan
    public async Task Injection_type_in_use_anywhere_is_deactivated_not_deleted(string usedBy)
    {
        await using var db = NewContext();
        var injection = new InjectionType { InjectionName = "B12", Unit = "mcg" };
        db.InjectionTypes.Add(injection);
        await db.SaveChangesAsync();
        if (usedBy == "package") db.PackageItems.Add(new PackageItem { PackageId = 1, InjectionId = injection.Id, WeekNumber = 1 });
        if (usedBy == "given") db.WeeklyAdministeredItems.Add(new WeeklyAdministeredItem { FollowUpId = 1, InjectionId = injection.Id, ActualQuantity = 1 });
        if (usedBy == "planned") db.EnrollmentPlannedItems.Add(new EnrollmentPlannedItem { EnrollmentId = 1, WeekNumber = 2, InjectionId = injection.Id, Quantity = 1 });
        await db.SaveChangesAsync();

        await Service(db).DeleteAsync(CatalogueKind.Injection, injection.Id);

        Assert.False((await db.InjectionTypes.AsNoTracking().SingleAsync()).IsActive);
    }

    [Fact]
    public async Task Vitamin_type_used_by_a_package_is_deactivated_not_deleted()
    {
        await using var db = NewContext();
        var vitamin = new VitaminType { VitaminName = "Vitamin C", Unit = "mg" };
        db.VitaminTypes.Add(vitamin);
        await db.SaveChangesAsync();
        db.PackageItems.Add(new PackageItem { PackageId = 1, VitaminId = vitamin.Id, WeekNumber = 1 });
        await db.SaveChangesAsync();

        await Service(db).DeleteAsync(CatalogueKind.Vitamin, vitamin.Id);

        Assert.False((await db.VitaminTypes.AsNoTracking().SingleAsync()).IsActive);
    }

    [Fact]
    public async Task Analysis_type_used_by_a_lab_request_is_deactivated_not_deleted()
    {
        await using var db = NewContext();
        var analysis = new AnalysisType { TypeName = "CBC" };
        db.AnalysisTypes.Add(analysis);
        await db.SaveChangesAsync();
        db.MedicalAnalyses.Add(new MedicalAnalysis { AnalysisTypeId = analysis.Id, PatientId = 1, DoctorId = 1 });
        await db.SaveChangesAsync();

        await Service(db).DeleteAsync(CatalogueKind.Analysis, analysis.Id);

        Assert.False((await db.AnalysisTypes.AsNoTracking().SingleAsync()).IsActive);
        Assert.True(Assert.Single(await Service(db).ListAsync(CatalogueKind.Analysis)).InUse);
    }

    // ── Save: per-catalogue checks, own input model ──

    public static TheoryData<CatalogueKind, CatalogueItemForm> Refused => new()
    {
        { CatalogueKind.Treatment, new CatalogueItemForm { Name = "", Category = "InternalMedicine", Cost = 100, DurationMinutes = 30 } },
        { CatalogueKind.Treatment, new CatalogueItemForm { Name = "Visit", Category = "InternalMedicine", Cost = 0, DurationMinutes = 30 } },
        { CatalogueKind.Treatment, new CatalogueItemForm { Name = "Visit", Category = "InternalMedicine", Cost = -5, DurationMinutes = 30 } },
        { CatalogueKind.Treatment, new CatalogueItemForm { Name = "Visit", Category = "Surgery", Cost = 100, DurationMinutes = 30 } },
        { CatalogueKind.Treatment, new CatalogueItemForm { Name = "Visit", Category = "InternalMedicine", Cost = 100, DurationMinutes = 2 } },
        { CatalogueKind.Analysis, new CatalogueItemForm { Name = "CBC", Cost = -1 } },
        { CatalogueKind.Injection, new CatalogueItemForm { Name = "B12", Unit = " " } },
        { CatalogueKind.Vitamin, new CatalogueItemForm { Name = new string('x', 101), Unit = "mg" } },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public async Task Invalid_forms_are_refused_and_nothing_is_saved(CatalogueKind kind, CatalogueItemForm form)
    {
        await using var db = NewContext();

        var result = await Service(db).SaveAsync(kind, form);

        Assert.False(result.IsSuccess);
        Assert.Empty(await Service(db).ListAsync(kind));
    }

    [Fact]
    public async Task The_nutrition_follow_up_visit_may_be_free()
    {
        await using var db = NewContext();

        var result = await Service(db).SaveAsync(CatalogueKind.Treatment,
            new CatalogueItemForm { Name = "Follow-up", Category = "Nutritional", Cost = 0, DurationMinutes = 15 });

        Assert.True(result.IsSuccess, string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task A_duplicate_name_is_refused()
    {
        await using var db = NewContext();
        db.InjectionTypes.Add(new InjectionType { InjectionName = "B12", Unit = "ml" });
        await db.SaveChangesAsync();

        var result = await Service(db).SaveAsync(CatalogueKind.Injection, new CatalogueItemForm { Name = " b12 ", Unit = "ml" });

        Assert.False(result.IsSuccess);
        Assert.Single(await db.InjectionTypes.ToListAsync());
    }

    [Fact]
    public async Task Editing_changes_only_what_the_form_shows_and_can_reactivate()
    {
        await using var db = NewContext();
        var created = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var vitamin = new VitaminType { VitaminName = "Zinc", Unit = "tab", IsActive = false, CreatedAt = created };
        db.VitaminTypes.Add(vitamin);
        await db.SaveChangesAsync();

        var result = await Service(db).SaveAsync(CatalogueKind.Vitamin,
            new CatalogueItemForm { Id = vitamin.Id, Name = " Zinc 50 ", Unit = "tablet", Formulation = "Oral", IsActive = true });

        Assert.True(result.IsSuccess, string.Join(" ", result.Errors));
        var stored = await db.VitaminTypes.AsNoTracking().SingleAsync();
        Assert.Equal(("Zinc 50", "tablet", "Oral", true), (stored.VitaminName, stored.Unit, stored.Formulation!, stored.IsActive));
        Assert.Equal(created, stored.CreatedAt);
    }
}
