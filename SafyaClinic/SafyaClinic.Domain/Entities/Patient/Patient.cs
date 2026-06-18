using SafyaClinic.Domain.Entities.Analysis;
using SafyaClinic.Domain.Entities.Common;
using SafyaClinic.Domain.Entities.MedicalRecord;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;


namespace SafyaClinic.Domain.Entities.Patient
{
    public class Patient : AuditableEntity
    {
        public int? UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public Gender? Gender { get; set; }
        public BloodType? BloodType { get; set; }
        public string? NationalId { get; set; }
        public decimal? HeightCm { get; set; }              // For BMI calculation
        public string? Allergies { get; set; }
        public string? ChronicDiseases { get; set; }
        public string? Notes { get; set; }

        // Navigation properties
        public virtual User? User { get; set; }
        public virtual ICollection<PatientPhone> Phones { get; set; } = new List<PatientPhone>();
        public virtual ICollection<PatientAddress> Addresses { get; set; } = new List<PatientAddress>();
        public virtual ICollection<ReservationStatusEnum> Reservations { get; set; } = new List<ReservationStatusEnum>();
        public virtual ICollection<PatientRecord> Records { get; set; } = new List<PatientRecord>();
        public virtual ICollection<MedicalAnalysis> Analyses { get; set; } = new List<MedicalAnalysis>();
        public virtual ICollection<PatientNutritionEnrollment> NutritionEnrollments { get; set; } = new List<PatientNutritionEnrollment>();
        public virtual ICollection<Payment.Payment> Payments { get; set; } = new List<Payment.Payment>();
    }
}
