using SafyaClinic.Domain.Entities.Common;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;
using System.ComponentModel.DataAnnotations.Schema;


namespace SafyaClinic.Domain.Entities.Payment
{
    public class Payment:BaseEntity
    {
        public int? ReservationId { get; set; }
        public int PatientId { get; set; }
        public int? EnrollmentId { get; set; }              // NEW: For nutrition package payments
        [ForeignKey("Collector")]
        public int CollectedBy { get; set; }
        public decimal Amount { get; set; }
        public PaymentMethodEnum PaymentMethod { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ── Clinic / Source attribution ─────────────────────────
        public int? ClinicId { get; set; }
        public int? PatientSourceId { get; set; }
        public bool IsFirstVisitDeduction { get; set; }          // true if this payment applied the first-visit source/clinic deduction
        public decimal? DeductionPercentage { get; set; }        // % applied (snapshot at time of payment)
        public decimal SourceDeductionAmount { get; set; }       // amount attributed to the source/clinic agreement
        public decimal ClinicNetAmount { get; set; }             // Amount - SourceDeductionAmount (what the clinic actually keeps)

        // ── Status / cancellation ───────────────────────────────
        public PaymentStatusEnum Status { get; set; } = PaymentStatusEnum.Active;
        public DateTime? CancelledAt { get; set; }
        public int? CancelledBy { get; set; }
        public string? CancellationReason { get; set; }

        // ── Amount-change tracking ──────────────────────────────
        public decimal? OriginalAmount { get; set; }             // Amount at time of collection, kept even after edits
        public DateTime? LastModifiedAt { get; set; }
        public int? LastModifiedBy { get; set; }

        // Navigation properties
        public virtual Reservation.Reservation? Reservation { get; set; }
        public virtual Patient.Patient Patient { get; set; } = null!;
        public virtual User Collector { get; set; } = null!;
        public virtual Nutrition.PatientNutritionEnrollment? Enrollment { get; set; }
        public virtual Settings.Clinic? Clinic { get; set; }
        public virtual Settings.PatientSource? PatientSource { get; set; }
        public virtual ICollection<PaymentAdjustment> Adjustments { get; set; } = new List<PaymentAdjustment>();
    }
}
