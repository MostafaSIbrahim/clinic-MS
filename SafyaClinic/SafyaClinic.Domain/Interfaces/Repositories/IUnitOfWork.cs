using SafyaClinic.Domain.Entities.Analysis;
using SafyaClinic.Domain.Entities.MedicalRecord;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Entities.Payment;
using SafyaClinic.Domain.Entities.Prescription;
using SafyaClinic.Domain.Entities.Reservation;
using SafyaClinic.Domain.Entities.Settings;
using SafyaClinic.Domain.Identity;

namespace SafyaClinic.Domain.Interfaces.Repositories;

public interface IUnitOfWork : IDisposable
{
    // ── Identity ─────────────────────────────────────────────
    IRepository<User> Users { get; }
    IRepository<Role> Roles { get; }
    IRepository<UserRole> UserRoles { get; }

    // ── Patient ──────────────────────────────────────────────
    IRepository<Patient> Patients { get; }
    IRepository<PatientPhone> PatientPhones { get; }
    IRepository<PatientAddress> PatientAddresses { get; }

    // ── Reservation ──────────────────────────────────────────
    IRepository<Reservation> Reservations { get; }
    IRepository<ReservationStatus> ReservationStatuses { get; }

    // ── Medical Record ────────────────────────────────────────
    IRepository<PatientRecord> PatientRecords { get; }
    IRepository<Treatment> Treatments { get; }
    IRepository<TreatmentType> TreatmentTypes { get; }

    // ── Prescription ──────────────────────────────────────────
    IRepository<Prescription> Prescriptions { get; }
    IRepository<PrescriptionAttachment> PrescriptionAttachments { get; }

    // ── Analysis ──────────────────────────────────────────────
    IRepository<AnalysisType> AnalysisTypes { get; }
    IRepository<MedicalAnalysis> MedicalAnalyses { get; }
    IRepository<AnalysisAttachment> AnalysisAttachments { get; }

    // ── Payment ───────────────────────────────────────────────
    IRepository<Payment> Payments { get; }
    IRepository<PaymentAdjustment> PaymentAdjustments { get; }

    // ── Settings (Sources / Clinics) ────────────────────────────
    IRepository<PatientSource> PatientSources { get; }
    IRepository<Clinic> Clinics { get; }
    IRepository<ClinicSourceAgreement> ClinicSourceAgreements { get; }

    // ── Nutrition ─────────────────────────────────────────────
    IRepository<InjectionType> InjectionTypes { get; }
    IRepository<VitaminType> VitaminTypes { get; }
    INutritionPackageRepository NutritionPackages { get; }
    IRepository<PackageItem> PackageItems { get; }
    IRepository<PatientNutritionEnrollment> NutritionEnrollments { get; }
    IWeeklyFollowUpRepository WeeklyFollowUps { get; }
    IRepository<WeeklyFollowUpLabResult> WeeklyFollowUpLabResults { get; }
    IRepository<WeeklyAdministeredItem> WeeklyAdministeredItems { get; }

    // ── Persistence ───────────────────────────────────────────
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}