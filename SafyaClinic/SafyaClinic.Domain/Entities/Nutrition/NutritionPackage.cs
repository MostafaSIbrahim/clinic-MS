using SafyaClinic.Domain.Entities.Common;


namespace SafyaClinic.Domain.Entities.Nutrition
{
    public class NutritionPackage:AuditableEntity
    {
        public string PackageName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DurationWeeks { get; set; } = 4;                // Fixed: 1 month
        public int SessionsPerWeek { get; set; } = 1;              // Once per week
        public decimal BasePrice { get; set; }
        public decimal MaxDiscountPercent { get; set; } = 20.00m;  // Max 20% discount
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<PackageItem> Items { get; set; } = new List<PackageItem>();
        public virtual ICollection<PatientNutritionEnrollment> Enrollments { get; set; } = new List<PatientNutritionEnrollment>();
    }
}
