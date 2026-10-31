using Microsoft.EntityFrameworkCore;
using SafyaClinic.Domain.Entities.Analysis;
using SafyaClinic.Domain.Entities.Audit;
using SafyaClinic.Domain.Entities.MedicalRecord;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Entities.Payment;
using SafyaClinic.Domain.Entities.Prescription;
using SafyaClinic.Domain.Entities.Reservation;
using SafyaClinic.Domain.Entities.Settings;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;


namespace SafyaClinic.Infrastructure.Data
{
    public class SafyaDbContext: DbContext
    {
       
        public SafyaDbContext(DbContextOptions<SafyaDbContext> options) : base(options)
        {
        }
        // Identity
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }

        // Patient
        public DbSet<Patient> Patients { get; set; }
        public DbSet<PatientPhone> PatientPhones { get; set; }
        public DbSet<PatientAddress> PatientAddresses { get; set; }

        // Reservation
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<ReservationStatus> ReservationStatuses { get; set; }

        // Medical Record
        public DbSet<PatientRecord> PatientRecords { get; set; }
        public DbSet<Treatment> Treatments { get; set; }
        public DbSet<TreatmentType> TreatmentTypes { get; set; }

        // Prescription
        public DbSet<Prescription> Prescriptions { get; set; }
        public DbSet<PrescriptionItem> PrescriptionItems { get; set; }
        public DbSet<PrescriptionAttachment> PrescriptionAttachments { get; set; }

        // Analysis
        public DbSet<AnalysisType> AnalysisTypes { get; set; }
        public DbSet<MedicalAnalysis> MedicalAnalyses { get; set; }
        public DbSet<AnalysisAttachment> AnalysisAttachments { get; set; }

        // Payment
        public DbSet<Payment> Payments { get; set; }
        public DbSet<PaymentAdjustment> PaymentAdjustments { get; set; }

        // Settings (sources / clinics / agreements)
        public DbSet<PatientSource> PatientSources { get; set; }
        public DbSet<Clinic> Clinics { get; set; }
        public DbSet<ClinicSourceAgreement> ClinicSourceAgreements { get; set; }

        // Nutrition
        public DbSet<InjectionType> InjectionTypes { get; set; }
        public DbSet<VitaminType> VitaminTypes { get; set; }
        public DbSet<NutritionPackage> NutritionPackages { get; set; }
        public DbSet<PackageItem> PackageItems { get; set; }
        public DbSet<PatientNutritionEnrollment> PatientNutritionEnrollments { get; set; }
        public DbSet<WeeklyFollowUp> WeeklyFollowUps { get; set; }
        public DbSet<WeeklyFollowUpLabResult> WeeklyFollowUpLabResults { get; set; }
        public DbSet<WeeklyAdministeredItem> WeeklyAdministeredItems { get; set; }

        // Audit
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Patient>()
                .Property(p => p.Weight)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<MedicalAnalysis>()
                .HasOne(ma=> ma.Type)
                .WithMany(at=> at.Analyses)
                .HasForeignKey(ma=> ma.AnalysisTypeId);

            modelBuilder.Entity<WeeklyAdministeredItem>()
                .HasOne(wi => wi.PackageItem)
                .WithMany(pi => pi.AdministeredItems)
                .HasForeignKey(wi => wi.PackageItemId);
            // Apply all configurations from this assembly
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(SafyaDbContext).Assembly);

        }
    }
}

