using SafyaClinic.Domain.Entities.Common;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;

namespace SafyaClinic.Domain.Entities.Nutrition
{
    public class PatientNutritionEnrollment:AuditableEntity
    {
        public int PatientId { get; set; }
        public int PackageId { get; set; }
        public int DoctorId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal BasePrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal FinalPrice => BasePrice * (1 - DiscountPercent / 100);
        public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
        public decimal TotalPaid { get; set; }
        public string? Notes { get; set; }

        // Navigation properties
        public virtual Patient.Patient Patient { get; set; } = null!;
        public virtual NutritionPackage Package { get; set; } = null!;
        public virtual User Doctor { get; set; } = null!;
        public virtual ICollection<WeeklyFollowUp> WeeklyFollowUps { get; set; } = new List<WeeklyFollowUp>();
        public virtual ICollection<Payment.Payment> Payments { get; set; } = new List<Payment.Payment>();
    }
}
