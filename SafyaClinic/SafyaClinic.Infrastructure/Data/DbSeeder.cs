using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Services;
using SafyaClinic.Domain.Entities.Analysis;
using SafyaClinic.Domain.Entities.MedicalRecord;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Entities.Reservation;
using SafyaClinic.Domain.Entities.Settings;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;

namespace SafyaClinic.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(SafyaDbContext context)
    {
        await SeedRolesAsync(context);
        await SeedAdminAsync(context);
        await SeedAnalysisTypesAsync(context);
        await SeedTreatmentTypesAsync(context);
        await SeedInjectionTypesAsync(context);
        await SeedVitaminTypesAsync(context);
        await SeedReservationStatusesAsync(context);
        await SeedClinicsAsync(context);
        await SeedPatientSourcesAsync(context);

    }

    // ── Roles ───────────────────────────────────────────────
    private static async Task SeedRolesAsync(SafyaDbContext context)
    {
        if (await context.Roles.AnyAsync()) return;

        var roles = new[]
        {
            new Role { RoleName = "Admin", Description = "System administrator with full access" },
            new Role { RoleName = "Doctor", Description = "Medical doctor with patient management access" },
            new Role { RoleName = "Reception", Description = "Receptionist with reservation and payment access" },
            new Role { RoleName = "Patient", Description = "Patient with self-service portal access" },
            new Role { RoleName = "Nutritionist", Description = "Specialized nutrition doctor" }
        };

        await context.Roles.AddRangeAsync(roles);
        await context.SaveChangesAsync();
    }

    private static async Task SeedAdminAsync(SafyaDbContext context)
    {
        if (await context.Users.AnyAsync()) return;

        var admin = new User
        {
            FullName = "System Administrator",
            Email = "admin@safya.com",
            PhoneNumber = "0100",
            PasswordHash = AuthService.HashPassword("Admin@123"),
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1)
        };

        await context.Users.AddAsync(admin);
        await context.SaveChangesAsync();

        // Update self-reference
        admin.CreatedBy = admin.Id;
        context.Users.Update(admin);

        // Assign admin role
        var adminRole = await context.Roles.FirstAsync(r => r.RoleName == "Admin");
        await context.UserRoles.AddAsync(new UserRole
        {
            UserId = admin.Id,
            RoleId = adminRole.Id,
            AssignedAt = new DateTime(2026, 1, 1),
            AssignedBy = admin.Id
        });

        await context.SaveChangesAsync();
    }

    // ── Analysis Types ────────────────────────────────────────

    private static async Task SeedAnalysisTypesAsync(SafyaDbContext context)
    {
        if (await context.AnalysisTypes.AnyAsync()) return;

        var types = new[]
        {
            new AnalysisType { TypeName = "Blood Test", Description = "Complete blood count and analysis", DefaultCost = 150.00m, PreparationInstructions = "Fasting 8-12 hours required" },
            new AnalysisType {  TypeName = "X-Ray", Description = "Radiographic imaging", DefaultCost = 200.00m, PreparationInstructions = "Remove metal objects" },
            new AnalysisType {  TypeName = "MRI", Description = "Magnetic resonance imaging", DefaultCost = 1500.00m, PreparationInstructions = "Remove all metal objects" },
            new AnalysisType {  TypeName = "CT Scan", Description = "Computed tomography scan", DefaultCost = 1200.00m, PreparationInstructions = "Fasting may be required" },
            new AnalysisType {  TypeName = "Ultrasound", Description = "Ultrasonic imaging", DefaultCost = 300.00m, PreparationInstructions = "Full bladder may be required" },
            new AnalysisType {  TypeName = "Urine Analysis", Description = "Urine sample analysis", DefaultCost = 100.00m, PreparationInstructions = "Clean catch midstream sample" },
            new AnalysisType {  TypeName = "Lipid Profile", Description = "Cholesterol and triglycerides", DefaultCost = 200.00m, PreparationInstructions = "Fasting 12 hours required" },
            new AnalysisType {  TypeName = "Liver Function", Description = "ALT, AST, bilirubin panel", DefaultCost = 250.00m, PreparationInstructions = "Fasting preferred" },
            new AnalysisType {  TypeName = "Kidney Function", Description = "Creatinine, BUN, eGFR", DefaultCost = 250.00m, PreparationInstructions = "No special preparation" },
            new AnalysisType {  TypeName = "Thyroid Panel", Description = "TSH, T3, T4", DefaultCost = 300.00m, PreparationInstructions = "Morning sample preferred" },
            new AnalysisType {  TypeName = "HbA1c", Description = "Glycated hemoglobin", DefaultCost = 180.00m, PreparationInstructions = "No fasting required" },
            new AnalysisType {  TypeName = "Vitamin D Level", Description = "25-hydroxyvitamin D", DefaultCost = 250.00m, PreparationInstructions = "No special preparation" },
            new AnalysisType {  TypeName = "Iron Panel", Description = "Ferritin, serum iron, TIBC", DefaultCost = 220.00m, PreparationInstructions = "Morning fasting preferred" },
            new AnalysisType {  TypeName = "Hormone Panel", Description = "Testosterone, estrogen, progesterone", DefaultCost = 400.00m, PreparationInstructions = "Cycle day 3 for women" }
        };

        await context.AnalysisTypes.AddRangeAsync(types);
        await context.SaveChangesAsync();
    }

    // ── Treatment Types ───────────────────────────────────────

    private static async Task SeedTreatmentTypesAsync(SafyaDbContext context)
    {
        if (await context.TreatmentTypes.AnyAsync()) return;

        var types = new[]
        {
            new TreatmentType { Category = TreatmentCategory.InternalMedicine, TypeName = "General Consultation", Description = "Initial examination and consultation", DefaultCost = 200.00m, DurationMinutes = 30 },
            new TreatmentType { Category = TreatmentCategory.InternalMedicine, TypeName = "Follow-up Visit", Description = "Routine follow-up examination", DefaultCost = 100.00m, DurationMinutes = 15 },
            new TreatmentType { Category = TreatmentCategory.InternalMedicine, TypeName = "Emergency Treatment", Description = "Urgent care treatment", DefaultCost = 500.00m, DurationMinutes = 60 },
            new TreatmentType { Category = TreatmentCategory.InternalMedicine, TypeName = "Procedure", Description = "Medical procedure", DefaultCost = 1000.00m, DurationMinutes = 90 },
            new TreatmentType { Category = TreatmentCategory.Nutritional, TypeName = "Nutrition Consultation", Description = "Initial nutritional assessment", DefaultCost = 300.00m, DurationMinutes = 45 },
            new TreatmentType { Category = TreatmentCategory.Nutritional, TypeName = "Diet Plan Review", Description = "Weekly diet plan review", DefaultCost = 150.00m, DurationMinutes = 20 },
            new TreatmentType { Category = TreatmentCategory.Nutritional, TypeName = "Body Composition Analysis", Description = "InBody/body composition test", DefaultCost = 200.00m, DurationMinutes = 15 }
        };

        await context.TreatmentTypes.AddRangeAsync(types);
        await context.SaveChangesAsync();
    }

    // ── Injection Types ───────────────────────────────────────

    private static async Task SeedInjectionTypesAsync(SafyaDbContext context)
    {
        if (await context.InjectionTypes.AnyAsync()) return;

        var types = new[]
        {
            new InjectionType {  InjectionName = "Lipo-C", Unit = "ml", Description = "Lipotropic compound injection", DefaultDosage = "2.0 ml" },
            new InjectionType {  InjectionName = "B-Complex", Unit = "ml", Description = "Vitamin B complex injection", DefaultDosage = "1.0 ml" },
            new InjectionType {  InjectionName = "B12 (Methylcobalamin)", Unit = "mcg", Description = "Vitamin B12 injection", DefaultDosage = "1000 mcg" },
            new InjectionType {  InjectionName = "Glutathione", Unit = "mg", Description = "Antioxidant injection", DefaultDosage = "600 mg" },
            new InjectionType {  InjectionName = "Vitamin D3", Unit = "IU", Description = "Vitamin D3 injection", DefaultDosage = "50000 IU" },
            new InjectionType {  InjectionName = "MIC Injection", Unit = "ml", Description = "Methionine Inositol Choline", DefaultDosage = "2.0 ml" }
        };

        await context.InjectionTypes.AddRangeAsync(types);
        await context.SaveChangesAsync();
    }

    // ── Vitamin Types ─────────────────────────────────────────

    private static async Task SeedVitaminTypesAsync(SafyaDbContext context)
    {
        if (await context.VitaminTypes.AnyAsync()) return;

        var types = new[]
        {
            new VitaminType { VitaminName = "Vitamin C", Formulation = "IV Drip", Unit = "mg", Description = "High dose Vitamin C infusion" },
            new VitaminType {  VitaminName = "Vitamin B12", Formulation = "Injectable", Unit = "mcg", Description = "Methylcobalamin injection" },
            new VitaminType { VitaminName = "Vitamin D3", Formulation = "Injectable", Unit = "IU", Description = "Cholecalciferol injection" },
            new VitaminType { VitaminName = "Glutathione", Formulation = "Injectable", Unit = "mg", Description = "Master antioxidant" },
            new VitaminType { VitaminName = "Multi-Vitamin Complex", Formulation = "IV Drip", Unit = "ml", Description = "Complete vitamin complex infusion" },
            new VitaminType { VitaminName = "Zinc Sulfate", Formulation = "Injectable", Unit = "mg", Description = "Zinc supplementation" }
        };

        await context.VitaminTypes.AddRangeAsync(types);
        await context.SaveChangesAsync();
    }

    // ── Reservation Statuses ────────────────────────────────────

    private static async Task SeedReservationStatusesAsync(SafyaDbContext context)
    {
        if (await context.ReservationStatuses.AnyAsync()) return;

        var statuses = new[]
        {
            new ReservationStatus { StatusName = "Pending", Description = "Reservation awaiting confirmation", ColorCode = "#ffc107" },
            new ReservationStatus { StatusName = "Confirmed", Description = "Reservation confirmed", ColorCode = "#17a2b8" },
            new ReservationStatus { StatusName = "Completed", Description = "Patient visit completed", ColorCode = "#28a745" },
            new ReservationStatus { StatusName = "Cancelled", Description = "Reservation cancelled", ColorCode = "#dc3545" },
            new ReservationStatus { StatusName = "NoShow", Description = "Patient did not show up", ColorCode = "#6c757d" }
        };

        await context.ReservationStatuses.AddRangeAsync(statuses);
        await context.SaveChangesAsync();
    }

    // ── Clinics ────────────────────────────────────────────────

    private static async Task SeedClinicsAsync(SafyaDbContext context)
    {
        if (await context.Clinics.AnyAsync()) return;

        await context.Clinics.AddAsync(new Clinic
        {
            Name = "Main Clinic",
            Address = string.Empty,
            IsActive = true
        });

        await context.SaveChangesAsync();
    }

    // ── Patient Sources ───────────────────────────────────────

    private static async Task SeedPatientSourcesAsync(SafyaDbContext context)
    {
        if (await context.PatientSources.AnyAsync()) return;

        var sources = new[]
        {
            new PatientSource { Name = "Walk-in", Description = "Patient came directly with no referral", DefaultDeductionPercentage = 0m },
            new PatientSource { Name = "Vezeeta", Description = "Booked via Vezeeta platform", DefaultDeductionPercentage = 20m },
            new PatientSource { Name = "Ekshef", Description = "Booked via Ekshef platform", DefaultDeductionPercentage = 20m },
            new PatientSource { Name = "Instagram", Description = "Came via Instagram", DefaultDeductionPercentage = 10m },
            new PatientSource { Name = "Facebook", Description = "Came via Facebook", DefaultDeductionPercentage = 10m },
            new PatientSource { Name = "Marketing Campaign", Description = "Came via a paid marketing campaign", DefaultDeductionPercentage = 15m }
        };

        await context.PatientSources.AddRangeAsync(sources);
        await context.SaveChangesAsync();
    }

   
}