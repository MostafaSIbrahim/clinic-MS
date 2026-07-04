namespace SafyaClinic.Application.DTOs.Payment;

public class PaymentDto
{
    public int Id { get; init; }
    public int PatientId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public int? ReservationId { get; init; }
    public int? EnrollmentId { get; init; }
    public string CollectorName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public DateTime PaymentDate { get; init; }
    public string? ReferenceNumber { get; init; }
    public string? Notes { get; init; }
}

public class CollectPaymentRequest
{
    public int PatientId { get; init; }
    public int? ReservationId { get; init; }
    public int? EnrollmentId { get; init; }
    public int CollectedBy { get; init; }
    public decimal Amount { get; init; }
    public string PaymentMethod { get; init; } = "Cash";
    public string? ReferenceNumber { get; init; }
    public string? Notes { get; init; }
}

public class PatientFinancialSummaryDto
{
    public int PatientId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public decimal TotalCharged { get; init; }
    public decimal TotalPaid { get; init; }
    public decimal Balance => TotalCharged - TotalPaid;
    public IEnumerable<PaymentDto> Payments { get; init; } = Enumerable.Empty<PaymentDto>();
}