using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SafyaClinic.Application.Common;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>F5b / D8: one normal form for phones; a primary phone belongs to one patient.</summary>
public class PatientPhoneTests
{
    [Theory]
    [InlineData("+20 100 123 4567", "01001234567")]
    [InlineData("0020-100-1234567", "01001234567")]
    [InlineData("٠١٠٠١٢٣٤٥٦٧", "01001234567")]
    [InlineData("(010) 0123-4567", "01001234567")]
    [InlineData("01001234567", "01001234567")]
    [InlineData("  ", "")]
    public void Phone_numbers_have_one_normal_form(string typed, string stored)
    {
        Assert.Equal(stored, PhoneNumbers.Normalize(typed));
    }

    [Fact]
    public void Primary_phone_numbers_are_unique_in_the_database()
    {
        var model = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseSqlServer("Server=model-only;Database=ModelOnly;Integrated Security=True").Options).Model;

        var index = Assert.Single(model.FindEntityType(typeof(PatientPhone))!.GetIndexes(),
            i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(PatientPhone.PhoneNumber));
        Assert.True(index.IsUnique);
        Assert.Equal("[IsPrimary] = 1", index.GetFilter());
    }

    private static async Task<SafyaDbContext> SeededAsync()
    {
        var context = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"phones-{Guid.NewGuid()}").Options);
        await DbSeeder.SeedAsync(context, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DbSeeder.AdminPasswordSettingKey] = "Phone-Test-Password-1" })
            .Build());
        context.ChangeTracker.Clear();
        return context;
    }

    private static PatientService Patients(SafyaDbContext context)
    {
        context.ChangeTracker.Clear();   // each call is a new request in the app
        return new PatientService(new UnitOfWork(context));
    }

    private static CreatePatientRequest Patient(string firstName, params (string Number, bool Primary)[] phones) => new()
    {
        FirstName = firstName,
        LastName = "Test",
        Phones = phones.Select(p => new CreatePatientPhoneRequest { PhoneNumber = p.Number, IsPrimary = p.Primary }).ToList()
    };

    [Fact]
    public async Task Same_primary_phone_in_another_format_is_refused()
    {
        await using var context = await SeededAsync();
        await Patients(context).CreatePatientAsync(Patient("First", ("01001234567", true)), 1);

        var second = await Patients(context).CreatePatientAsync(Patient("Second", ("+20 100 123 4567", true)), 1);

        Assert.False(second.IsSuccess);
        Assert.Contains("First Test", second.Errors.First());
    }

    [Fact]
    public async Task First_phone_becomes_primary_and_numbers_are_stored_normalised()
    {
        await using var context = await SeededAsync();

        await Patients(context).CreatePatientAsync(Patient("Only", ("010 0123 4567", false), ("+20 122 000 1111", false)), 1);

        var phones = await context.PatientPhones.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(new[] { "01001234567", "01220001111" }, phones.Select(p => p.PhoneNumber));
        Assert.Equal(new[] { true, false }, phones.Select(p => p.IsPrimary));
    }

    [Fact]
    public async Task A_secondary_phone_may_be_shared()
    {
        await using var context = await SeededAsync();
        await Patients(context).CreatePatientAsync(Patient("Parent", ("01001234567", true)), 1);

        var child = await Patients(context).CreatePatientAsync(Patient("Child", ("01009999999", true), ("01001234567", false)), 1);

        Assert.True(child.IsSuccess);
    }

    [Fact]
    public async Task A_phone_is_required()
    {
        await using var context = await SeededAsync();

        var result = await Patients(context).CreatePatientAsync(Patient("NoPhone"), 1);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Adding_a_primary_phone_moves_the_primary_flag()
    {
        await using var context = await SeededAsync();
        var created = await Patients(context).CreatePatientAsync(Patient("Mover", ("01001234567", true)), 1);
        var patientId = created.Data!.Id;

        await Patients(context).AddPhoneAsync(patientId, new CreatePatientPhoneRequest { PhoneNumber = "01112223333", IsPrimary = true });

        var primaries = await context.PatientPhones.AsNoTracking()
            .Where(p => p.PatientId == patientId && p.IsPrimary).Select(p => p.PhoneNumber).ToListAsync();
        Assert.Equal(new[] { "01112223333" }, primaries);
    }

    [Fact]
    public async Task The_primary_phone_cannot_be_removed()
    {
        await using var context = await SeededAsync();
        var created = await Patients(context).CreatePatientAsync(Patient("Keeper", ("01001234567", true)), 1);
        var phoneId = await context.PatientPhones.Where(p => p.PatientId == created.Data!.Id).Select(p => p.Id).SingleAsync();

        var result = await Patients(context).RemovePhoneAsync(created.Data!.Id, phoneId);

        Assert.False(result.IsSuccess);
    }

    // ── P7a: the address on the registration form is optional ─────

    [Fact]
    public async Task A_patient_can_be_registered_without_an_address()
    {
        var context = await SeededAsync();
        var request = Patient("NoAddress", ("01005550001", true));
        request.Addresses.Add(new CreatePatientAddressRequest());   // the form's empty address row

        var result = await Patients(context).CreatePatientAsync(request, 1);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.Empty(await context.PatientAddresses.Where(a => a.PatientId == result.Data!.Id).ToListAsync());
    }

    [Fact]
    public async Task A_started_address_needs_its_city()
    {
        var context = await SeededAsync();
        var request = Patient("HalfAddress", ("01005550002", true));
        request.Addresses.Add(new CreatePatientAddressRequest { Street = "12 Tahrir St" });

        var result = await Patients(context).CreatePatientAsync(request, 1);

        Assert.False(result.IsSuccess);
        Assert.Contains("city", result.Errors.First(), StringComparison.OrdinalIgnoreCase);
    }
}
