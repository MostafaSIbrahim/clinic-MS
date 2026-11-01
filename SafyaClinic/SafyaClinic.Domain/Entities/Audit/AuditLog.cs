using SafyaClinic.Domain.Entities.Common;

namespace SafyaClinic.Domain.Entities.Audit
{
    /// <summary>One security- or money-relevant event (F3b). Written by IAuditTrail; never updated.</summary>
    public class AuditLog : BaseEntity
    {
        public DateTime OccurredAtUtc { get; set; }
        public int? UserId { get; set; }                         // who did it; null when unknown (failed sign-in)
        public string Action { get; set; } = string.Empty;       // e.g. "User.Deactivated"
        public string EntityType { get; set; } = string.Empty;   // e.g. "User"
        public int? EntityId { get; set; }
        public string? Details { get; set; }                     // small JSON object; never passwords
        public string? IpAddress { get; set; }
        public string? RequestId { get; set; }                   // matches the logs and the error page
    }
}