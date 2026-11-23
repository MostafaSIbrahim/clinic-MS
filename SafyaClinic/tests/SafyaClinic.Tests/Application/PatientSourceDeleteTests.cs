using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Entities.Settings;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>
/// M5c: deleting a patient source. A source that patients came from is made inactive instead of deleted,
/// like one with payments, so the patients keep it; a source nobody uses is deleted.
/// </summary>
public class PatientSourceDeleteTests
{
    private static SafyaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<SafyaDbContext>().UseInMemoryDatabase($"sources-{Guid.NewGuid()}").Options);

    private static PatientSourceService Service(SafyaDbContext db)
    {
        db.ChangeTracker.Clear();
        return new PatientSourceService(new UnitOfWork(db));
    }

    [Fact]
    public async Task Source_used_by_a_patient_is_made_inactive_not_deleted()
    {
        await using var db = NewContext();
        var source = new PatientSource { Name = "Booking website", DefaultDeductionPercentage = 10 };
        db.PatientSources.Add(source);
        await db.SaveChangesAsync();
        db.Patients.Add(new Patient { FirstName = "Mona", LastName = "Adel", PatientSourceId = source.Id });
        await db.SaveChangesAsync();

        var result = await Service(db).DeleteAsync(source.Id);

        Assert.True(result.IsSuccess);
        Assert.Contains("made inactive", result.Message);
        var kept = await db.PatientSources.AsNoTracking().SingleAsync();
        Assert.False(kept.IsActive);
        Assert.Equal(source.Id, (await db.Patients.AsNoTracking().SingleAsync()).PatientSourceId);
    }

    [Fact]
    public async Task Unused_source_is_deleted()
    {
        await using var db = NewContext();
        var source = new PatientSource { Name = "Campaign", DefaultDeductionPercentage = 5 };
        db.PatientSources.Add(source);
        await db.SaveChangesAsync();

        var result = await Service(db).DeleteAsync(source.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(await db.PatientSources.AsNoTracking().ToListAsync());
    }
}
