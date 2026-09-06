// SafyaClinic.Domain/Entities/Prescription/Prescription.cs
using SafyaClinic.Domain.Entities.Common;

namespace SafyaClinic.Domain.Entities.Prescription;

public class Prescription : BaseEntity
{
    public int RecordId { get; set; }
    public DateTime PrescriptionDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public bool IsPrinted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedBy { get; set; }

    // Navigation
    public virtual MedicalRecord.PatientRecord Record { get; set; } = null!;
    public virtual ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
    public virtual ICollection<PrescriptionAttachment> Attachments { get; set; } = new List<PrescriptionAttachment>();
}