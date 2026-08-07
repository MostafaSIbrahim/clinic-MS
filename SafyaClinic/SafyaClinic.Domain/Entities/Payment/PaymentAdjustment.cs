using SafyaClinic.Domain.Entities.Common;

namespace SafyaClinic.Domain.Entities.Payment
{
    /// <summary>
    /// Audit trail entry recorded every time a payment amount is changed or a payment
    /// is cancelled, so the payment history stays fully traceable.
    /// </summary>
    public class PaymentAdjustment : BaseEntity
    {
        public int PaymentId { get; set; }
        public string ActionType { get; set; } = string.Empty; // "AmountChanged" | "Cancelled"
        public decimal? OldAmount { get; set; }
        public decimal? NewAmount { get; set; }
        public string? Reason { get; set; }
        public int PerformedBy { get; set; }
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Payment Payment { get; set; } = null!;
    }
}
