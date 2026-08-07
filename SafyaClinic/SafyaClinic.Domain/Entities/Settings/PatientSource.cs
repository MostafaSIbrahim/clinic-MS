using SafyaClinic.Domain.Entities.Common;

namespace SafyaClinic.Domain.Entities.Settings
{
    /// <summary>
    /// A flexible, admin-managed list of where patients come from
    /// (Vezeeta, Ekshef, Instagram, Facebook, Marketing campaign, ...).
    /// Admin can add/deactivate/delete sources at any time.
    /// </summary>
    public class PatientSource : AuditableEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        /// <summary>
        /// Default % deducted from the patient's fees on their FIRST reservation only,
        /// used when no specific Clinic-Source agreement exists for the booking clinic.
        /// </summary>
        public decimal DefaultDeductionPercentage { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<Patient.Patient> Patients { get; set; } = new List<Patient.Patient>();
        public virtual ICollection<ClinicSourceAgreement> ClinicAgreements { get; set; } = new List<ClinicSourceAgreement>();
        public virtual ICollection<Payment.Payment> Payments { get; set; } = new List<Payment.Payment>();
    }
}
