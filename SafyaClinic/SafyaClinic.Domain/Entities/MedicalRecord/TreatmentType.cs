using SafyaClinic.Domain.Entities.Common;
using SafyaClinic.Domain.Enums;


namespace SafyaClinic.Domain.Entities.MedicalRecord
{
    public class TreatmentType:BaseEntity
    {
        public TreatmentCategory Category { get; set; } = TreatmentCategory.InternalMedicine;
        public string TypeName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? DefaultCost { get; set; }
        public int DurationMinutes { get; set; } = 30;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<Treatment> Treatments { get; set; } = new List<Treatment>();
    }
}
