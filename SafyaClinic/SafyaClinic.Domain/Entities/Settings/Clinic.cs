using SafyaClinic.Domain.Entities.Common;

namespace SafyaClinic.Domain.Entities.Settings
{
    /// <summary>
    /// A physical clinic/branch where a reservation/treatment takes place.
    /// Each clinic has its own agreement (deduction %) per patient source.
    /// </summary>
    public class Clinic : AuditableEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<Reservation.Reservation> Reservations { get; set; } = new List<Reservation.Reservation>();
        public virtual ICollection<ClinicSourceAgreement> SourceAgreements { get; set; } = new List<ClinicSourceAgreement>();
        public virtual ICollection<Payment.Payment> Payments { get; set; } = new List<Payment.Payment>();
    }
}
