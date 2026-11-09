using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Common;
using SafyaClinic.Application.Common.Auditing;
using SafyaClinic.Application.DTOs.MedicalRecord;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.MedicalRecord;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Entities.Reservation;
using SafyaClinic.Domain.Entities.Settings;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Infrastructure.Repositories;
using Xunit;

namespace SafyaClinic.Tests.Application;

/// <summary>
/// P5c: Start consultation offers the patient's latest record of the same kind; a visit can continue in it
/// (a record covers several visits) or get a new record.
/// </summary>
public class ContinueRecordTests
{
    private sealed class NoContext : IAuditContext
    {
        public int? UserId => 1;
        public string? IpAddress => null;
        public string? RequestId => null;
    }

    private sealed record World(SafyaDbContext Db, int Karim, int Mona, int Patient, int OtherPatient, int Visit);

    private static async Task<World> WorldAsync(PatientQueueStatus queue = PatientQueueStatus.InConsultation)
    {
        var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"continue-{Guid.NewGuid()}").Options);
        var karim = new User { FullName = "Dr Karim", PhoneNumber = "0601", PasswordHash = "x", ClinicianType = ClinicianType.Doctor };
        var mona = new User { FullName = "Dr Mona", PhoneNumber = "0602", PasswordHash = "x", ClinicianType = ClinicianType.Doctor };
        var patient = new Patient { FirstName = "Mona", LastName = "Adel" };
        var other = new Patient { FirstName = "Ali", LastName = "Hassan" };
        var confirmed = new ReservationStatus { StatusName = "Confirmed" };
        db.AddRange(karim, mona, patient, other, confirmed, new Clinic { Name = "Main" },
            new TreatmentType { TypeName = "General Consultation" });
        await db.SaveChangesAsync();
        var visit = new Reservation
        {
            PatientId = patient.Id, DoctorId = karim.Id, ClinicId = 1, StatusId = confirmed.Id, TreatmentTypeId = 1,
            ReservationDate = new DateTime(2026, 11, 9), ReservationTime = new TimeSpan(10, 0, 0),
            Category = TreatmentCategory.InternalMedicine, QueueStatus = queue,
        };
        db.Add(visit);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new World(db, karim.Id, mona.Id, patient.Id, other.Id, visit.Id);
    }

    private static async Task<int> RecordAsync(World w, int doctorId, DateTime created,
        TreatmentCategory category = TreatmentCategory.InternalMedicine, bool locked = false, int? patientId = null)
    {
        var record = new PatientRecord
        {
            PatientId = patientId ?? w.Patient, DoctorId = doctorId, Category = category, IsLocked = locked,
            CreatedAt = created, Diagnosis = "Hypertension",
        };
        w.Db.Add(record);
        await w.Db.SaveChangesAsync();
        w.Db.ChangeTracker.Clear();
        return record.Id;
    }

    private static PatientRecordService Records(World w)
    {
        w.Db.ChangeTracker.Clear();
        var uow = new UnitOfWork(w.Db);
        return new PatientRecordService(uow, new AuditTrail(uow, new NoContext()));
    }

    private static ReservationService Reservations(World w)
    {
        w.Db.ChangeTracker.Clear();
        var clock = new ClinicClock(ClinicClock.FindZone(ClinicClock.DefaultTimeZoneId), () => new DateTime(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc));
        return new ReservationService(new UnitOfWork(w.Db), new SafyaClinic.Infrastructure.Security.PermissionStore(w.Db), clock);
    }

    private static Task<int?> LinkAsync(World w) =>
        w.Db.Reservations.AsNoTracking().Where(r => r.Id == w.Visit).Select(r => r.MedicalRecordId).SingleAsync();

    [Fact]
    public async Task The_latest_record_of_the_same_kind_is_offered_from_any_doctor()
    {
        var w = await WorldAsync();
        await RecordAsync(w, w.Karim, new DateTime(2026, 9, 1));
        var latest = await RecordAsync(w, w.Mona, new DateTime(2026, 10, 1));
        await RecordAsync(w, w.Karim, new DateTime(2026, 11, 1), TreatmentCategory.Nutritional);
        await RecordAsync(w, w.Karim, new DateTime(2026, 11, 2), patientId: w.OtherPatient);

        var found = await Records(w).GetLatestRecordAsync(w.Patient, "InternalMedicine");

        Assert.Equal(latest, found!.Id);
        Assert.Equal("Dr Mona", found.DoctorName);
        Assert.Null(await Records(w).GetLatestRecordAsync(w.OtherPatient, "Nutritional"));
    }

    [Fact]
    public async Task A_locked_latest_record_is_still_the_one_offered()
    {
        var w = await WorldAsync();
        await RecordAsync(w, w.Karim, new DateTime(2026, 9, 1));
        var locked = await RecordAsync(w, w.Karim, new DateTime(2026, 10, 1), locked: true);

        var found = await Records(w).GetLatestRecordAsync(w.Patient, "InternalMedicine");

        Assert.Equal(locked, found!.Id);
        Assert.True(found.IsLocked);   // the page then offers only a new record
    }

    [Fact]
    public async Task Continuing_links_the_visit_to_the_record_and_is_audited()
    {
        var w = await WorldAsync();
        var record = await RecordAsync(w, w.Mona, new DateTime(2026, 10, 1));   // another doctor's record

        var result = await Records(w).ContinueRecordAsync(w.Visit, record, w.Karim, isAdmin: false);

        Assert.True(result.IsSuccess);
        Assert.Equal(record, await LinkAsync(w));
        Assert.Equal(w.Visit, await Reservations(w).GetVisitInConsultationAsync(record));
        var entry = Assert.Single(await w.Db.AuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal(AuditActions.RecordVisitLinked, entry.Action);
        var visits = (await Records(w).GetRecordByIdAsync(record)).Data!.Visits;
        Assert.Equal(w.Visit, Assert.Single(visits).ReservationId);
    }

    [Fact]
    public async Task A_locked_record_another_kind_or_another_patients_record_cannot_be_continued()
    {
        var w = await WorldAsync();
        var locked = await RecordAsync(w, w.Karim, new DateTime(2026, 10, 1), locked: true);
        var nutrition = await RecordAsync(w, w.Karim, new DateTime(2026, 10, 2), TreatmentCategory.Nutritional);
        var someoneElse = await RecordAsync(w, w.Karim, new DateTime(2026, 10, 3), patientId: w.OtherPatient);

        foreach (var record in new[] { locked, nutrition, someoneElse })
            Assert.False((await Records(w).ContinueRecordAsync(w.Visit, record, w.Karim, isAdmin: false)).IsSuccess);
        Assert.Null(await LinkAsync(w));
    }

    [Fact]
    public async Task Only_the_visit_doctor_or_an_admin_can_continue_while_in_consultation()
    {
        var w = await WorldAsync();
        var record = await RecordAsync(w, w.Karim, new DateTime(2026, 10, 1));

        Assert.False((await Records(w).ContinueRecordAsync(w.Visit, record, w.Mona, isAdmin: false)).IsSuccess);
        Assert.True((await Records(w).ContinueRecordAsync(w.Visit, record, w.Mona, isAdmin: true)).IsSuccess);

        var waiting = await WorldAsync(PatientQueueStatus.Waiting);
        var waitingRecord = await RecordAsync(waiting, waiting.Karim, new DateTime(2026, 10, 1));
        Assert.False((await Records(waiting).ContinueRecordAsync(waiting.Visit, waitingRecord, waiting.Karim, isAdmin: false)).IsSuccess);
    }

    [Fact]
    public async Task A_visit_is_linked_once_and_a_new_record_links_its_visit()
    {
        var w = await WorldAsync();
        var created = await Records(w).CreateRecordAsync(new CreatePatientRecordRequest
        {
            PatientId = w.Patient, DoctorId = w.Karim, ReservationId = w.Visit, Category = "InternalMedicine",
        }, w.Karim, isAdmin: false);
        Assert.True(created.IsSuccess);
        Assert.Equal(created.Data!.Id, await LinkAsync(w));

        var older = await RecordAsync(w, w.Karim, new DateTime(2026, 10, 1));
        Assert.False((await Records(w).ContinueRecordAsync(w.Visit, older, w.Karim, isAdmin: false)).IsSuccess);
        Assert.Equal(created.Data.Id, await LinkAsync(w));
    }
}
