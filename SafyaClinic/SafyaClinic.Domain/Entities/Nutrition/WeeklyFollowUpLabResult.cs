

using SafyaClinic.Domain.Entities.Common;

namespace SafyaClinic.Domain.Entities.Nutrition
{
    public class WeeklyFollowUpLabResult: BaseEntity
    {
        public int FollowUpId { get; set; }
        public int AnalysisTypeId { get; set; }
        public string? ResultValue { get; set; }
        public string? ReferenceRange { get; set; }
        public bool? IsNormal { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual WeeklyFollowUp FollowUp { get; set; } = null!;
        public virtual Analysis.AnalysisType AnalysisType { get; set; } = null!;
    }
}
