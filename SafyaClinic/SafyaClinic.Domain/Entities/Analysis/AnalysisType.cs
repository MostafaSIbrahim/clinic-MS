using SafyaClinic.Domain.Entities.Common;

namespace SafyaClinic.Domain.Entities.Analysis
{
    public class AnalysisType:BaseEntity
    {
        public string TypeName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? DefaultCost { get; set; }
        public string? PreparationInstructions { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<MedicalAnalysis> Analyses { get; set; } = new List<MedicalAnalysis>();
    }
}
