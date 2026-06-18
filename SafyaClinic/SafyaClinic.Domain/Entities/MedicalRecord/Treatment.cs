using SafyaClinic.Domain.Entities.Common;

namespace SafyaClinic.Domain.Entities.MedicalRecord
{
    public class Treatment:BaseEntity
    {
        public int RecordId { get; set; }
        public int? TreatmentTypeId { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal? Cost { get; set; }
        public DateTime PerformedDate { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int CreatedBy { get; set; }

        // Navigation properties
        public virtual PatientRecord Record { get; set; } = null!;
        public virtual TreatmentType? Type { get; set; }
    }
}
