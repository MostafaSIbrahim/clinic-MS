using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SafyaClinic.Domain.Entities.MedicalRecord;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Entities.Reservation;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Web.Controllers;
using Xunit;

namespace SafyaClinic.Tests.Web.Lookups;

/// <summary>F33: "in use" means referenced by reservations / package items.</summary>
public class LookupsControllerDeleteTests
{
    private sealed class NoTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static SafyaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"lookups-{Guid.NewGuid()}")
            .Options);

    private static LookupsController NewController(SafyaDbContext context)
    {
        var httpContext = new DefaultHttpContext();
        return new LookupsController(context, NullLogger<LookupsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new NoTempDataProvider())
        };
    }

    [Fact]
    public async Task Treatment_type_used_by_a_reservation_is_deactivated_not_deleted()
    {
        await using var context = NewContext();
        var type = new TreatmentType { TypeName = "Consultation" };
        context.TreatmentTypes.Add(type);
        await context.SaveChangesAsync();
        context.Reservations.Add(new Reservation { TreatmentTypeId = type.Id, PatientId = 1, DoctorId = 1, ClinicId = 1, StatusId = 1 });
        await context.SaveChangesAsync();

        await NewController(context).DeleteTreatmentType(type.Id);

        var stored = await context.TreatmentTypes.AsNoTracking().SingleAsync();
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task Unused_treatment_type_is_deleted()
    {
        await using var context = NewContext();
        var type = new TreatmentType { TypeName = "Unused" };
        context.TreatmentTypes.Add(type);
        await context.SaveChangesAsync();

        await NewController(context).DeleteTreatmentType(type.Id);

        Assert.False(await context.TreatmentTypes.AnyAsync());
    }

    [Fact]
    public async Task Injection_type_used_by_a_package_is_deactivated_not_deleted()
    {
        await using var context = NewContext();
        var injection = new InjectionType { InjectionName = "B12", Unit = "mcg" };
        context.InjectionTypes.Add(injection);
        await context.SaveChangesAsync();
        context.PackageItems.Add(new PackageItem { PackageId = 1, InjectionId = injection.Id, WeekNumber = 1 });
        await context.SaveChangesAsync();

        await NewController(context).DeleteInjectionType(injection.Id);

        Assert.False((await context.InjectionTypes.AsNoTracking().SingleAsync()).IsActive);
    }

    [Fact]
    public async Task Vitamin_type_used_by_a_package_is_deactivated_not_deleted()
    {
        await using var context = NewContext();
        var vitamin = new VitaminType { VitaminName = "Vitamin C", Unit = "mg" };
        context.VitaminTypes.Add(vitamin);
        await context.SaveChangesAsync();
        context.PackageItems.Add(new PackageItem { PackageId = 1, VitaminId = vitamin.Id, WeekNumber = 1 });
        await context.SaveChangesAsync();

        await NewController(context).DeleteVitaminType(vitamin.Id);

        Assert.False((await context.VitaminTypes.AsNoTracking().SingleAsync()).IsActive);
    }
}