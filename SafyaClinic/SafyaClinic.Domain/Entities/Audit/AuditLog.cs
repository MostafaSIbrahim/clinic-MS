using SafyaClinic.Domain.Entities.Common;

namespace SafyaClinic.Domain.Entities.Audit
{
    public class AuditLog:BaseEntity
    {
        public string TableName { get; set; } = string.Empty;
        public int RecordId { get; set; }
        public string Action { get; set; } = string.Empty;          // INSERT, UPDATE, DELETE
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public int PerformedBy { get; set; }
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
        public string? IPAddress { get; set; }
    }
}
