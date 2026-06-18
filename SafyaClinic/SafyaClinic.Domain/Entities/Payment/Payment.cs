using SafyaClinic.Domain.Entities.Common;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;


namespace SafyaClinic.Domain.Entities.Payment
{
    public class Payment:BaseEntity
    {
        public int? ReservationId { get; set; }
        public int PatientId { get; set; }
        public int? EnrollmentId { get; set; }              // NEW: For nutrition package payments
        public int CollectedBy { get; set; }
        public decimal Amount { get; set; }
        public PaymentMethodEnum PaymentMethod { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Reservation.Reservation? Reservation { get; set; }
        public virtual Patient.Patient Patient { get; set; } = null!;
        public virtual User Collector { get; set; } = null!;
        public virtual Nutrition.PatientNutritionEnrollment? Enrollment { get; set; }
    }
}
