using Microsoft.EntityFrameworkCore;
using SafyaClinic.Domain.Entities.Analysis;
using SafyaClinic.Domain.Entities.Audit;
using SafyaClinic.Domain.Entities.MedicalRecord;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Entities.Payment;
using SafyaClinic.Domain.Entities.Prescription;
using SafyaClinic.Domain.Entities.Reservation;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;


namespace SafyaClinic.Infrastructure.Data
{
    internal class SafyaDbContext: DbContext
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
        public DbSet<PrescriptionAttachment> PrescriptionAttachments { get; set; }

        // Analysis
        public DbSet<AnalysisType> AnalysisTypes { get; set; }
        public DbSet<MedicalAnalysis> MedicalAnalyses { get; set; }
        public DbSet<AnalysisAttachment> AnalysisAttachments { get; set; }

        // Payment
        public DbSet<Payment> Payments { get; set; }

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

            // Apply all configurations from this assembly
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(SafyaDbContext).Assembly);

            // Seed data
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed Roles
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, RoleName = "Admin", Description = "System administrator with full access" },
                new Role { Id = 2, RoleName = "Doctor", Description = "Medical doctor with patient management access" },
                new Role { Id = 3, RoleName = "Reception", Description = "Receptionist with reservation and payment access" },
                new Role { Id = 4, RoleName = "Patient", Description = "Patient with self-service portal access" },
                new Role { Id = 5, RoleName = "Nutritionist", Description = "Specialized nutrition doctor" }
            );

            // Seed Reservation Statuses
            modelBuilder.Entity<ReservationStatus>().HasData(
                new ReservationStatus { Id = 1, StatusName = "Pending", Description = "Reservation awaiting confirmation", ColorCode = "#ffc107" },
                new ReservationStatus { Id = 2, StatusName = "Confirmed", Description = "Reservation confirmed", ColorCode = "#17a2b8" },
                new ReservationStatus { Id = 3, StatusName = "Completed", Description = "Patient visit completed", ColorCode = "#28a745" },
                new ReservationStatus { Id = 4, StatusName = "Cancelled", Description = "Reservation cancelled", ColorCode = "#dc3545" },
                new ReservationStatus { Id = 5, StatusName = "NoShow", Description = "Patient did not show up", ColorCode = "#6c757d" }
            );

            // Seed Treatment Types - Internal Medicine
            modelBuilder.Entity<TreatmentType>().HasData(
                new TreatmentType { Id = 1, Category = TreatmentCategory.InternalMedicine, TypeName = "General Consultation", Description = "Initial examination and consultation", DefaultCost = 200.00m, DurationMinutes = 30 },
                new TreatmentType { Id = 2, Category = TreatmentCategory.InternalMedicine, TypeName = "Follow-up Visit", Description = "Routine follow-up examination", DefaultCost = 100.00m, DurationMinutes = 15 },
                new TreatmentType { Id = 3, Category = TreatmentCategory.InternalMedicine, TypeName = "Emergency Treatment", Description = "Urgent care treatment", DefaultCost = 500.00m, DurationMinutes = 60 },
                new TreatmentType { Id = 4, Category = TreatmentCategory.InternalMedicine, TypeName = "Procedure", Description = "Medical procedure", DefaultCost = 1000.00m, DurationMinutes = 90 }
            );

            // Seed Treatment Types - Nutritional
            modelBuilder.Entity<TreatmentType>().HasData(
                new TreatmentType { Id = 5, Category = TreatmentCategory.Nutritional, TypeName = "Nutrition Consultation", Description = "Initial nutritional assessment", DefaultCost = 300.00m, DurationMinutes = 45 },
                new TreatmentType { Id = 6, Category = TreatmentCategory.Nutritional, TypeName = "Diet Plan Review", Description = "Weekly diet plan review", DefaultCost = 150.00m, DurationMinutes = 20 },
                new TreatmentType { Id = 7, Category = TreatmentCategory.Nutritional, TypeName = "Body Composition Analysis", Description = "InBody/body composition test", DefaultCost = 200.00m, DurationMinutes = 15 }
            );

            // Seed Injection Types
            modelBuilder.Entity<InjectionType>().HasData(
                new InjectionType { Id = 1, InjectionName = "Lipo-C", Unit = "ml", Description = "Lipotropic compound injection", DefaultDosage = "2.0 ml" },
                new InjectionType { Id = 2, InjectionName = "B-Complex", Unit = "ml", Description = "Vitamin B complex injection", DefaultDosage = "1.0 ml" },
                new InjectionType { Id = 3, InjectionName = "B12 (Methylcobalamin)", Unit = "mcg", Description = "Vitamin B12 injection", DefaultDosage = "1000 mcg" },
                new InjectionType { Id = 4, InjectionName = "Glutathione", Unit = "mg", Description = "Antioxidant injection", DefaultDosage = "600 mg" },
                new InjectionType { Id = 5, InjectionName = "Vitamin D3", Unit = "IU", Description = "Vitamin D3 injection", DefaultDosage = "50000 IU" },
                new InjectionType { Id = 6, InjectionName = "MIC Injection", Unit = "ml", Description = "Methionine Inositol Choline", DefaultDosage = "2.0 ml" }
            );

            // Seed Vitamin Types
            modelBuilder.Entity<VitaminType>().HasData(
                new VitaminType { Id = 1, VitaminName = "Vitamin C", Formulation = "IV Drip", Unit = "mg", Description = "High dose Vitamin C infusion" },
                new VitaminType { Id = 2, VitaminName = "Vitamin B12", Formulation = "Injectable", Unit = "mcg", Description = "Methylcobalamin injection" },
                new VitaminType { Id = 3, VitaminName = "Vitamin D3", Formulation = "Injectable", Unit = "IU", Description = "Cholecalciferol injection" },
                new VitaminType { Id = 4, VitaminName = "Glutathione", Formulation = "Injectable", Unit = "mg", Description = "Master antioxidant" },
                new VitaminType { Id = 5, VitaminName = "Multi-Vitamin Complex", Formulation = "IV Drip", Unit = "ml", Description = "Complete vitamin complex infusion" },
                new VitaminType { Id = 6, VitaminName = "Zinc Sulfate", Formulation = "Injectable", Unit = "mg", Description = "Zinc supplementation" }
            );

            // Seed Analysis Types
            modelBuilder.Entity<AnalysisType>().HasData(
                new AnalysisType { Id = 1, TypeName = "Blood Test", Description = "Complete blood count and analysis", DefaultCost = 150.00m, PreparationInstructions = "Fasting 8-12 hours required" },
                new AnalysisType { Id = 2, TypeName = "X-Ray", Description = "Radiographic imaging", DefaultCost = 200.00m, PreparationInstructions = "Remove metal objects" },
                new AnalysisType { Id = 3, TypeName = "MRI", Description = "Magnetic resonance imaging", DefaultCost = 1500.00m, PreparationInstructions = "Remove all metal objects" },
                new AnalysisType { Id = 4, TypeName = "CT Scan", Description = "Computed tomography scan", DefaultCost = 1200.00m, PreparationInstructions = "Fasting may be required" },
                new AnalysisType { Id = 5, TypeName = "Ultrasound", Description = "Ultrasonic imaging", DefaultCost = 300.00m, PreparationInstructions = "Full bladder may be required" },
                new AnalysisType { Id = 6, TypeName = "Urine Analysis", Description = "Urine sample analysis", DefaultCost = 100.00m, PreparationInstructions = "Clean catch midstream sample" },
                new AnalysisType { Id = 7, TypeName = "Lipid Profile", Description = "Cholesterol and triglycerides", DefaultCost = 200.00m, PreparationInstructions = "Fasting 12 hours required" },
                new AnalysisType { Id = 8, TypeName = "Liver Function", Description = "ALT, AST, bilirubin panel", DefaultCost = 250.00m, PreparationInstructions = "Fasting preferred" },
                new AnalysisType { Id = 9, TypeName = "Kidney Function", Description = "Creatinine, BUN, eGFR", DefaultCost = 250.00m, PreparationInstructions = "No special preparation" },
                new AnalysisType { Id = 10, TypeName = "Thyroid Panel", Description = "TSH, T3, T4", DefaultCost = 300.00m, PreparationInstructions = "Morning sample preferred" },
                new AnalysisType { Id = 11, TypeName = "HbA1c", Description = "Glycated hemoglobin", DefaultCost = 180.00m, PreparationInstructions = "No fasting required" },
                new AnalysisType { Id = 12, TypeName = "Vitamin D Level", Description = "25-hydroxyvitamin D", DefaultCost = 250.00m, PreparationInstructions = "No special preparation" },
                new AnalysisType { Id = 13, TypeName = "Iron Panel", Description = "Ferritin, serum iron, TIBC", DefaultCost = 220.00m, PreparationInstructions = "Morning fasting preferred" },
                new AnalysisType { Id = 14, TypeName = "Hormone Panel", Description = "Testosterone, estrogen, progesterone", DefaultCost = 400.00m, PreparationInstructions = "Cycle day 3 for women" }
            );

            // Seed default Admin user
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    FullName = "System Administrator",
                    Email = "admin@safya.com",
                    PhoneNumber = "01000000000",
                    PasswordHash = "AQAAAAEAACcQAAAAEEdV0C/7L8Z1Z3Q3Q3Q3Q3Q3Q3Q3Q3Q3Q3Q3Q3Q3Q3Q3Q3Q3Q3Q3Q3Q3Q3Q3Q3Q==",
                    IsActive = true,
                    CreatedAt = new DateTime(2026, 1, 1),
                    CreatedBy = 1
                }
            );

            // Assign Admin role
            modelBuilder.Entity<UserRole>().HasData(
                new UserRole { Id = 1, UserId = 1, RoleId = 1, AssignedAt = new DateTime(2026, 1, 1) }
            );
        }
    }
}

