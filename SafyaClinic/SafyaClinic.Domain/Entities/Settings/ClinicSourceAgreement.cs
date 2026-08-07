using SafyaClinic.Domain.Entities.Common;

namespace SafyaClinic.Domain.Entities.Settings
{
    /// <summary>
    /// Defines the agreed deduction percentage between a specific Clinic and a specific
    /// PatientSource. This overrides PatientSource.DefaultDeductionPercentage for that clinic.
    /// Applied only to a patient's first reservation payment at that clinic.
    /// </summary>
    public class ClinicSourceAgreement : AuditableEntity
    {
        public int ClinicId { get; set; }
        public int PatientSourceId { get; set; }
        public decimal DeductionPercentage { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Notes { get; set; }

        // Navigation properties
        public virtual Clinic Clinic { get; set; } = null!;
        public virtual PatientSource PatientSource { get; set; } = null!;
    }
}
