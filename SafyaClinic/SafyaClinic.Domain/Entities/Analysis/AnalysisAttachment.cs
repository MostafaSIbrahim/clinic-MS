using SafyaClinic.Domain.Entities.Common;


namespace SafyaClinic.Domain.Entities.Analysis
{
    public class AnalysisAttachment:BaseEntity
    {
        public int AnalysisId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? FileType { get; set; }
        public int? FileSize { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public int UploadedBy { get; set; }

        // Navigation properties
        public virtual MedicalAnalysis Analysis { get; set; } = null!;
    }
}
