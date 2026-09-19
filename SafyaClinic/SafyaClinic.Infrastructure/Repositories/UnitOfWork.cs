using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SafyaClinic.Domain.Entities.Analysis;
using SafyaClinic.Domain.Entities.MedicalRecord;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Entities.Payment;
using SafyaClinic.Domain.Entities.Prescription;
using SafyaClinic.Domain.Entities.Reservation;
using SafyaClinic.Domain.Entities.Settings;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Domain.Interfaces.Repositories;
using SafyaClinic.Infrastructure.Data;
using System.Data;

namespace SafyaClinic.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly SafyaDbContext _context;
    private bool _disposed;

    // ── Identity ─────────────────────────────────────────────
    private IRepository<User>? _users;
    private IRepository<Role>? _roles;
    private IRepository<UserRole>? _userRoles;

    // ── Patient ──────────────────────────────────────────────
    private IRepository<Patient>? _patients;
    private IRepository<PatientPhone>? _patientPhones;
    private IRepository<PatientAddress>? _patientAddresses;

    // ── Reservation ──────────────────────────────────────────
    private IRepository<Reservation>? _reservations;
    private IRepository<ReservationStatus>? _reservationStatuses;

    // ── Medical Record ────────────────────────────────────────
    private IRepository<PatientRecord>? _patientRecords;
    private IRepository<Treatment>? _treatments;
    private IRepository<TreatmentType>? _treatmentTypes;

    // ── Prescription ──────────────────────────────────────────
    private IRepository<Prescription>? _prescriptions;
    private IRepository<PrescriptionItem>? _prescriptionsItem;
    private IRepository<PrescriptionAttachment>? _prescriptionAttachments;

    // ── Analysis ──────────────────────────────────────────────
    private IRepository<AnalysisType>? _analysisTypes;
    private IRepository<MedicalAnalysis>? _medicalAnalyses;
    private IRepository<AnalysisAttachment>? _analysisAttachments;

    // ── Payment ───────────────────────────────────────────────
    private IRepository<Payment>? _payments;
    private IRepository<PaymentAdjustment>? _paymentAdjustments;

    // ── Settings (Sources / Clinics) ────────────────────────────
    private IRepository<PatientSource>? _patientSources;
    private IRepository<Clinic>? _clinics;
    private IRepository<ClinicSourceAgreement>? _clinicSourceAgreements;

    // ── Nutrition ─────────────────────────────────────────────
    private IRepository<InjectionType>? _injectionTypes;
    private IRepository<VitaminType>? _vitaminTypes;
    private INutritionPackageRepository? _nutritionPackages;
    private IRepository<PackageItem>? _packageItems;
    private IRepository<PatientNutritionEnrollment>? _nutritionEnrollments;
    private IWeeklyFollowUpRepository? _weeklyFollowUps;
    private IRepository<WeeklyFollowUpLabResult>? _weeklyFollowUpLabResults;
    private IRepository<WeeklyAdministeredItem>? _weeklyAdministeredItems;

    public UnitOfWork(SafyaDbContext context) => _context = context;

    // ── Repository accessors (lazy-init) ──────────────────────

    public IRepository<User> Users => _users ??= new GenericRepository<User>(_context);
    public IRepository<Role> Roles => _roles ??= new GenericRepository<Role>(_context);
    public IRepository<UserRole> UserRoles => _userRoles ??= new GenericRepository<UserRole>(_context);

    public IRepository<Patient> Patients => _patients ??= new GenericRepository<Patient>(_context);
    public IRepository<PatientPhone> PatientPhones => _patientPhones ??= new GenericRepository<PatientPhone>(_context);
    public IRepository<PatientAddress> PatientAddresses => _patientAddresses ??= new GenericRepository<PatientAddress>(_context);

    public IRepository<Reservation> Reservations => _reservations ??= new GenericRepository<Reservation>(_context);
    public IRepository<ReservationStatus> ReservationStatuses => _reservationStatuses ??= new GenericRepository<ReservationStatus>(_context);

    public IRepository<PatientRecord> PatientRecords => _patientRecords ??= new GenericRepository<PatientRecord>(_context);
    public IRepository<Treatment> Treatments => _treatments ??= new GenericRepository<Treatment>(_context);
    public IRepository<TreatmentType> TreatmentTypes => _treatmentTypes ??= new GenericRepository<TreatmentType>(_context);

    public IRepository<Prescription> Prescriptions => _prescriptions ??= new GenericRepository<Prescription>(_context);
    public IRepository<PrescriptionItem> PrescriptionItems => _prescriptionsItem ??= new GenericRepository<PrescriptionItem>(_context);
    public IRepository<PrescriptionAttachment> PrescriptionAttachments => _prescriptionAttachments ??= new GenericRepository<PrescriptionAttachment>(_context);

    public IRepository<AnalysisType> AnalysisTypes => _analysisTypes ??= new GenericRepository<AnalysisType>(_context);
    public IRepository<MedicalAnalysis> MedicalAnalyses => _medicalAnalyses ??= new GenericRepository<MedicalAnalysis>(_context);
    public IRepository<AnalysisAttachment> AnalysisAttachments => _analysisAttachments ??= new GenericRepository<AnalysisAttachment>(_context);

    public IRepository<Payment> Payments => _payments ??= new GenericRepository<Payment>(_context);
    public IRepository<PaymentAdjustment> PaymentAdjustments => _paymentAdjustments ??= new GenericRepository<PaymentAdjustment>(_context);

    public IRepository<PatientSource> PatientSources => _patientSources ??= new GenericRepository<PatientSource>(_context);
    public IRepository<Clinic> Clinics => _clinics ??= new GenericRepository<Clinic>(_context);
    public IRepository<ClinicSourceAgreement> ClinicSourceAgreements => _clinicSourceAgreements ??= new GenericRepository<ClinicSourceAgreement>(_context);

    public IRepository<InjectionType> InjectionTypes => _injectionTypes ??= new GenericRepository<InjectionType>(_context);
    public IRepository<VitaminType> VitaminTypes => _vitaminTypes ??= new GenericRepository<VitaminType>(_context);
    public INutritionPackageRepository NutritionPackages => _nutritionPackages ??= new NutritionPackageRepository(_context);
    public IRepository<PackageItem> PackageItems => _packageItems ??= new GenericRepository<PackageItem>(_context);
    public IRepository<PatientNutritionEnrollment> NutritionEnrollments => _nutritionEnrollments ??= new GenericRepository<PatientNutritionEnrollment>(_context);
    public IWeeklyFollowUpRepository WeeklyFollowUps => _weeklyFollowUps ??= new WeeklyFollowUpRepository(_context);
    public IRepository<WeeklyFollowUpLabResult> WeeklyFollowUpLabResults => _weeklyFollowUpLabResults ??= new GenericRepository<WeeklyFollowUpLabResult>(_context);
    public IRepository<WeeklyAdministeredItem> WeeklyAdministeredItems => _weeklyAdministeredItems ??= new GenericRepository<WeeklyAdministeredItem>(_context);

    // ── Persistence ───────────────────────────────────────────

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
    public Task<T> ExecutePatientPaymentAsync<T>(
    int patientId,
    Func<Task<T>> operation,
    Func<T, bool> shouldCommit)
    {
        return ExecuteLockedAsync(
            $"SafyaClinic:PatientPayment:{patientId}",
            operation,
            shouldCommit);
    }

    public Task<T> ExecuteReservationRecordAsync<T>(
        int reservationId,
        Func<Task<T>> operation,
        Func<T, bool> shouldCommit)
    {
        return ExecuteLockedAsync(
            $"SafyaClinic:ReservationRecord:{reservationId}",
            operation,
            shouldCommit);
    }

    private async Task<T> ExecuteLockedAsync<T>(
        string resourceName,
        Func<Task<T>> operation,
        Func<T, bool> shouldCommit)
    {
        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted);

        try
        {
            var resource = new SqlParameter(
                "@resource",
                SqlDbType.NVarChar,
                255)
            {
                Value = resourceName
            };

            var lockResult = new SqlParameter(
                "@lockResult",
                SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };

            await _context.Database.ExecuteSqlRawAsync(
                """
            EXEC @lockResult = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 10000;
            """,
                resource,
                lockResult);

            if ((int)lockResult.Value < 0)
            {
                throw new TimeoutException(
                    "Could not acquire the operation lock.");
            }

            var result = await operation();

            if (shouldCommit(result))
            {
                await transaction.CommitAsync();
            }
            else
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
            }

            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    // ── Disposal ──────────────────────────────────────────────

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
            _context.Dispose();
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}