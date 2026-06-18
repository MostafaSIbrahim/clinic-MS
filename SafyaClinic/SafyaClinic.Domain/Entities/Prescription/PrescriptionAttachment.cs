using SafyaClinic.Domain.Entities.Common;


namespace SafyaClinic.Domain.Entities.Prescription
{
    public class PrescriptionAttachment:BaseEntity
    {
        public int PrescriptionId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? FileType { get; set; }
        public int? FileSize { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public int UploadedBy { get; set; }

        // Navigation properties
        public virtual Prescription Prescription { get; set; } = null!;
    }
}
