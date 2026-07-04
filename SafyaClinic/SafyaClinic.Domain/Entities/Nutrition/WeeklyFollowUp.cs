
using SafyaClinic.Domain.Entities.Common;
using SafyaClinic.Domain.Enums;

namespace SafyaClinic.Domain.Entities.Nutrition
{
    public class WeeklyFollowUp: BaseEntity
    {
        public int UpdatedBy;
        public DateTime? UpdatedAt;
        public int EnrollmentId { get; set; }
        public int WeekNumber { get; set; }                         // 1, 2, 3, 4
        public DateTime FollowUpDate { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? HeightCm { get; set; }
        public decimal? BMI {  get; private set; }
        public decimal? BodyFatPercent { get; set; }
        public decimal? MuscleMassKg { get; set; }
        public decimal? WaistCircumferenceCm { get; set; }
        public int? BloodPressureSys { get; set; }
        public int? BloodPressureDia { get; set; }
        public string? LabResultsSummary { get; set; }
        public string? DoctorNotes { get; set; }
        public DietCompliance? DietCompliance { get; set; }
        public string? SideEffects { get; set; }
        public string? NextWeekAdjustments { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int CreatedBy { get; set; }


        // Navigation properties
        public virtual PatientNutritionEnrollment Enrollment { get; set; } = null!;
        public virtual ICollection<WeeklyFollowUpLabResult> LabResults { get; set; } = new List<WeeklyFollowUpLabResult>();
        public virtual ICollection<WeeklyAdministeredItem> AdministeredItems { get; set; } = new List<WeeklyAdministeredItem>();
    }
}
