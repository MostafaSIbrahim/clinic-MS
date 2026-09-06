// SafyaClinic.Domain/Entities/Prescription/PrescriptionItem.cs
using SafyaClinic.Domain.Entities.Common;

namespace SafyaClinic.Domain.Entities.Prescription;

public class PrescriptionItem : BaseEntity
{
    public int PrescriptionId { get; set; }
    public string MedicationName { get; set; } = string.Empty;
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? RouteOfAdministration { get; set; }
    public string? Instructions { get; set; }

    // Navigation
    public virtual Prescription Prescription { get; set; } = null!;
}