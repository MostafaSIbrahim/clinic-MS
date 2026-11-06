using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Clinical;
using SafyaClinic.Application.Common.Auditing;
using SafyaClinic.Application.DTOs.MedicalRecord;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.MedicalRecord;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Entities.Prescription;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>P5a: prescribing a medicine that matches a recorded allergy needs the doctor's confirmation, which is audited.</summary>
public class AllergyCheckTests
{
    private static string Found(IEnumerable<AllergyMatch> matches) => string.Join("|", matches.Select(m => $"{m.Medication}={m.Allergy}"));

    [Fact]
    public void Allergies_are_split_on_commas_semicolons_the_Arabic_comma_new_lines_and_and()
    {
        Assert.Equal(new[] { "penicillin", "sulfa", "aspirin", "ibuprofen", "latex", "codeine" },
            AllergyCheck.Terms("Penicillin, Sulfa; aspirin، ibuprofen\nlatex and codeine"));
    }

    [Theory]
    [InlineData("NKDA")]
    [InlineData("none")]
    [InlineData("No known allergies")]
    [InlineData("لا يوجد")]
    [InlineData("ab, x")]
    [InlineData("  ")]
    [InlineData(null)]
    public void None_entries_and_very_short_words_are_ignored(string? allergies) =>
        Assert.Empty(AllergyCheck.Terms(allergies));

    [Fact]
    public void A_medicine_containing_an_allergy_matches_ignoring_case() =>
        Assert.Equal("PENICILLIN V 500mg=penicillin", Found(AllergyCheck.Matches("Penicillin", new[] { "PENICILLIN V 500mg", "Paracetamol" })));

    [Fact]
    public void An_allergy_naming_the_medicine_matches() =>
        Assert.Equal("Augmentin 1g=augmentin tablets", Found(AllergyCheck.Matches("Augmentin tablets", new[] { "Augmentin 1g" })));

    [Fact]
    public void Unrelated_medicines_or_no_allergies_give_no_warning()
    {
        Assert.Empty(AllergyCheck.Matches("Penicillin", new[] { "Paracetamol", "", null }));
        Assert.Empty(AllergyCheck.Matches(null, new[] { "Penicillin" }));
    }

    // ── The service enforces it ────────────────────────────────

    private sealed class NoContext : IAuditContext
    {
        public int? UserId => 1;
        public string? IpAddress => null;
        public string? RequestId => null;
    }

    private sealed record World(SafyaDbContext Db, int Record, int Prescription);

    private static async Task<World> WorldAsync(string? allergies = "Penicillin, sulfa")
    {
        var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"allergy-{Guid.NewGuid()}").Options);
        var doctor = new User { FullName = "Dr Karim", PhoneNumber = "0501", PasswordHash = "x" };
        var patient = new Patient { FirstName = "Mona", LastName = "Adel", Allergies = allergies };
        db.AddRange(doctor, patient);
        await db.SaveChangesAsync();
        var record = new PatientRecord { PatientId = patient.Id, DoctorId = doctor.Id };
        db.Add(record);
        await db.SaveChangesAsync();
        var prescription = new Prescription { RecordId = record.Id, CreatedBy = doctor.Id };
        db.Add(prescription);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new World(db, record.Id, prescription.Id);
    }

    private static PatientRecordService Service(World w)
    {
        w.Db.ChangeTracker.Clear();
        var uow = new UnitOfWork(w.Db);
        return new PatientRecordService(uow, new AuditTrail(uow, new NoContext()));
    }

    [Fact]
    public async Task Adding_a_matching_medicine_is_refused_without_confirmation()
    {
        var w = await WorldAsync();

        var result = await Service(w).AddPrescriptionItemAsync(w.Prescription,
            new AddPrescriptionItemRequest { MedicationName = "Penicillin V 500mg" });

        Assert.False(result.IsSuccess);
        Assert.True(AllergyCheck.IsWarning(result.Errors.First()));
        Assert.False(await w.Db.PrescriptionItems.AnyAsync());
        Assert.False(await w.Db.AuditLogs.AnyAsync());
    }

    [Fact]
    public async Task Prescribe_anyway_adds_it_and_records_the_override()
    {
        var w = await WorldAsync();

        var result = await Service(w).AddPrescriptionItemAsync(w.Prescription,
            new AddPrescriptionItemRequest { MedicationName = "Penicillin V 500mg", ConfirmAllergyOverride = true });

        Assert.True(result.IsSuccess);
        Assert.True(await w.Db.PrescriptionItems.AnyAsync(i => i.MedicationName == "Penicillin V 500mg"));
        var entry = Assert.Single(await w.Db.AuditLogs.ToListAsync());
        Assert.Equal(AuditActions.AllergyOverride, entry.Action);
        Assert.Equal(w.Prescription, entry.EntityId);
    }

    [Fact]
    public async Task Unrelated_medicines_need_no_confirmation_and_are_not_audited()
    {
        var w = await WorldAsync();

        var result = await Service(w).AddPrescriptionItemAsync(w.Prescription,
            new AddPrescriptionItemRequest { MedicationName = "Paracetamol 500mg" });

        Assert.True(result.IsSuccess);
        Assert.False(await w.Db.AuditLogs.AnyAsync());
    }

    [Fact]
    public async Task A_new_prescription_with_a_matching_medicine_is_refused_then_saved_when_confirmed()
    {
        var w = await WorldAsync();
        var items = new List<PrescriptionItemRequest>
        {
            new() { MedicationName = "Paracetamol 500mg" },
            new() { MedicationName = "Sulfamethoxazole" },
        };

        var refused = await Service(w).CreatePrescriptionAsync(new CreatePrescriptionRequest { RecordId = w.Record, Items = items }, 1);
        Assert.False(refused.IsSuccess);
        Assert.Contains("Sulfamethoxazole", refused.Errors.First());
        Assert.Equal(1, await w.Db.Prescriptions.CountAsync());   // only the one from the setup

        var saved = await Service(w).CreatePrescriptionAsync(
            new CreatePrescriptionRequest { RecordId = w.Record, Items = items, ConfirmAllergyOverride = true }, 1);
        Assert.True(saved.IsSuccess);
        Assert.Equal(2, await w.Db.Prescriptions.CountAsync());
        var entry = Assert.Single(await w.Db.AuditLogs.ToListAsync());
        Assert.Equal(AuditActions.AllergyOverride, entry.Action);
        Assert.Equal(w.Record, entry.EntityId);
    }
}
