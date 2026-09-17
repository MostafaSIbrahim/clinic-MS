using SafyaClinic.Application.DTOs.Common;

namespace SafyaClinic.Application.DTOs.Payment;

public class PaymentAdjustmentDto
{
    public int Id { get; init; }

    public string ActionType { get; init; } = string.Empty;

    public decimal? OldAmount { get; init; }

    public decimal? NewAmount { get; init; }

    public string? Reason { get; init; }

    public int PerformedBy { get; init; }

    public string PerformedByName { get; init; } = string.Empty;

    public DateTime PerformedAt { get; init; }
}

public class PaymentAuditDto
{
    public PaymentDto Payment { get; init; } = new();

    public PagedResult<PaymentAdjustmentDto> Adjustments { get; init; } = new()
    {
        Page = 1,
        PageSize = 20
    };
}