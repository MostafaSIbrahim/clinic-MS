using SafyaClinic.Domain.Entities.Common;


namespace SafyaClinic.Domain.Entities.Prescription
{
    public class Prescription:BaseEntity
    {
        public int RecordId { get; set; }
        public string MedicationName { get; set; } = string.Empty;
        public string? Dosage { get; set; }
        public string? Frequency { get; set; }
        public string? Duration { get; set; }
        public string? RouteOfAdministration { get; set; }
        public string? Instructions { get; set; }
        public bool IsPrinted { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int CreatedBy { get; set; }

        // Navigation properties
        public virtual MedicalRecord.PatientRecord Record { get; set; } = null!;
        public virtual ICollection<PrescriptionAttachment> Attachments { get; set; } = new List<PrescriptionAttachment>();
    }
}
