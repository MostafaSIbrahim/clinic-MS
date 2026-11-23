using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafyaClinic.Application.Common.Auditing;
using SafyaClinic.Application.Settings;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>
/// M5d: the clinic profile. Until it is saved, printouts use the ClinicInfo setting; once saved, there is one row,
/// which later saves update. The English name, address and a phone are required.
/// </summary>
public class ClinicProfileTests
{
    private static SafyaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<SafyaDbContext>().UseInMemoryDatabase($"profile-{Guid.NewGuid()}").Options);

    private static ClinicProfileService Service(SafyaDbContext db)
    {
        db.ChangeTracker.Clear();
        var defaults = Options.Create(new ClinicInfoOptions
        {
            Name = "Safya Clinic", Tagline = "Internal Medicine", Address = "123 Corniche St", Phone = "+20 2 1234 5678", Email = "info@safyaclinic.example",
        });
        return new ClinicProfileService(new UnitOfWork(db), NoAuditTrail.Instance, defaults);
    }

    private static ClinicProfileForm Valid() => new()
    {
        NameEn = "  Safya Polyclinic ", NameAr = "مجمع صفية الطبي", AddressEn = "14 Abbas El Akkad, Nasr City",
        AddressAr = "١٤ عباس العقاد، مدينة نصر", Phone = "+20 2 2401 0000", Email = "info@safya.clinic", LicenceLine = "Licence 12345",
    };

    [Fact]
    public async Task Before_saving_the_ClinicInfo_setting_is_used()
    {
        await using var db = NewContext();
        var p = await Service(db).GetAsync();

        Assert.False(p.IsSaved);
        Assert.Equal("Safya Clinic", p.NameEn);
        Assert.Equal("123 Corniche St", p.AddressEn);
        Assert.Null(p.NameAr);
    }

    [Fact]
    public async Task Saving_twice_keeps_one_profile_with_the_latest_details()
    {
        await using var db = NewContext();
        Assert.True((await Service(db).SaveAsync(Valid(), userId: 1)).IsSuccess);
        var second = Valid();
        second.Phone = "+20 2 2401 1111";
        second.SecondPhone = "0100 123 4567";
        Assert.True((await Service(db).SaveAsync(second, userId: 1)).IsSuccess);

        var row = await db.ClinicProfiles.AsNoTracking().SingleAsync();
        Assert.Equal("Safya Polyclinic", row.NameEn);   // trimmed
        Assert.Equal("+20 2 2401 1111", row.Phone);
        Assert.NotNull(row.UpdatedAt);

        var p = await Service(db).GetAsync();
        Assert.True(p.IsSaved);
        Assert.Equal("مجمع صفية الطبي", p.NameAr);
        Assert.Equal(new[] { "+20 2 2401 1111", "0100 123 4567" }, p.Phones);
    }

    [Theory]
    [InlineData(nameof(ClinicProfileForm.NameEn), "")]
    [InlineData(nameof(ClinicProfileForm.AddressEn), " ")]
    [InlineData(nameof(ClinicProfileForm.Phone), "")]
    [InlineData(nameof(ClinicProfileForm.Phone), "call us")]
    [InlineData(nameof(ClinicProfileForm.SecondPhone), "abc")]
    [InlineData(nameof(ClinicProfileForm.Email), "not-an-email")]
    public async Task Invalid_forms_are_refused(string field, string value)
    {
        await using var db = NewContext();
        var form = Valid();
        typeof(ClinicProfileForm).GetProperty(field)!.SetValue(form, value);

        Assert.True(ClinicProfileService.Check(form).ContainsKey(field));
        Assert.False((await Service(db).SaveAsync(form, userId: 1)).IsSuccess);
        Assert.Empty(await db.ClinicProfiles.AsNoTracking().ToListAsync());
    }

    [Fact]
    public void The_Arabic_fields_email_and_licence_are_optional()
    {
        var form = new ClinicProfileForm { NameEn = "Safya", AddressEn = "Nasr City", Phone = "02 2401 0000" };
        Assert.Empty(ClinicProfileService.Check(form));
    }
}
