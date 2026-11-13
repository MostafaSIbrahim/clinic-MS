using SafyaClinic.Domain.Entities.Common;

namespace SafyaClinic.Domain.Entities.Nutrition
{
    /// <summary>
    /// M4d: one item planned for a coming week of one patient's enrollment. It names exactly one item: a package
    /// item, an injection type, a vitamin type, or a drug typed by name (outside the package). The plan belongs to
    /// the patient; it never changes the package. That week's follow-up form starts from it.
    /// </summary>
    public class EnrollmentPlannedItem : BaseEntity
    {
        public int EnrollmentId { get; set; }
        public int WeekNumber { get; set; }                 // 1–4
        public int? PackageItemId { get; set; }
        public int? InjectionId { get; set; }
        public int? VitaminId { get; set; }
        public string? CustomName { get; set; }            // a drug outside the catalogues
        public decimal Quantity { get; set; }
        public string? Unit { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int CreatedBy { get; set; }

        public virtual PatientNutritionEnrollment Enrollment { get; set; } = null!;
        public virtual PackageItem? PackageItem { get; set; }
        public virtual InjectionType? Injection { get; set; }
        public virtual VitaminType? Vitamin { get; set; }
    }
}
