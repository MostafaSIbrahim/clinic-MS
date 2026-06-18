using SafyaClinic.Domain.Entities.Common;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;


namespace SafyaClinic.Domain.Entities.MedicalRecord
{
    public class PatientRecord:AuditableEntity
    {
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public int? ReservationId { get; set; }
        public TreatmentCategory Category { get; set; } = TreatmentCategory.InternalMedicine;
        public string? ChiefComplaint { get; set; }
        public string? PresentIllnessHistory { get; set; }
        public string? Diagnosis { get; set; }
        public string? DifferentialDiagnosis { get; set; }
        public string? TreatmentPlan { get; set; }
        public string? Notes { get; set; }
        public DateTime? FollowUpDate { get; set; }
        public bool IsLocked { get; set; }

        // Navigation properties
        public virtual Patient.Patient Patient { get; set; } = null!;
        public virtual User Doctor { get; set; } = null!;
        public virtual Reservation.Reservation? Reservation { get; set; }
        public virtual ICollection<Treatment> Treatments { get; set; } = new List<Treatment>();
        public virtual ICollection<Prescription.Prescription> Prescriptions { get; set; } = new List<Prescription.Prescription>();
    }
}
