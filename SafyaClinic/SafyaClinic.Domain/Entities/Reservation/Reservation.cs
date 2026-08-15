using SafyaClinic.Domain.Entities.Common;
using SafyaClinic.Domain.Entities.MedicalRecord;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;


namespace SafyaClinic.Domain.Entities.Reservation
{
    public class Reservation : AuditableEntity
    {
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public int ClinicId { get; set; }
        public int StatusId { get; set; }
        public int TreatmentTypeId { get; set; }
        public TreatmentCategory Category { get; set; } = TreatmentCategory.InternalMedicine;
        public DateTime ReservationDate { get; set; }
        public TimeSpan ReservationTime { get; set; }
        public int DurationMinutes { get; set; } = 30;
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public bool IsPaid { get; set; }
        public decimal? TotalAmount { get; set; }

        // Navigation properties
        public virtual Patient.Patient Patient { get; set; } = null!;
        public virtual User Doctor { get; set; } = null!;
        public virtual Settings.Clinic Clinic { get; set; } = null!;
        public virtual ReservationStatus Status { get; set; } = null!;
        public virtual TreatmentType TreatmentType { get; set; } = null!;
        public virtual PatientRecord? PatientRecord { get; set; }
        public virtual ICollection<Payment.Payment> Payments { get; set; } = new List<Payment.Payment>();
    }
}
