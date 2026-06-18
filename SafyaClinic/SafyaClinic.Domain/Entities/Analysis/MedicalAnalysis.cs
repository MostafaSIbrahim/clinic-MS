

using SafyaClinic.Domain.Entities.Common;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Identity;

namespace SafyaClinic.Domain.Entities.Analysis
{
    public class MedicalAnalysis:AuditableEntity
    {
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public int? RecordId { get; set; }
        public int AnalysisTypeId { get; set; }
        public AnalysisStatus Status { get; set; } = AnalysisStatus.Requested;
        public DateTime RequestDate { get; set; } = DateTime.UtcNow;
        public DateTime? ResultDate { get; set; }
        public string? ResultNotes { get; set; }
        public bool IsUrgent { get; set; }

        // Navigation properties
        public virtual Patient.Patient Patient { get; set; } = null!;
        public virtual User Doctor { get; set; } = null!;
        public virtual MedicalRecord.PatientRecord? Record { get; set; }
        public virtual AnalysisType Type { get; set; } = null!;
        public virtual ICollection<AnalysisAttachment> Attachments { get; set; } = new List<AnalysisAttachment>();
    }
}
