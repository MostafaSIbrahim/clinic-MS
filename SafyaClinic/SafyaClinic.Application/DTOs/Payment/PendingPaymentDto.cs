namespace SafyaClinic.Application.DTOs.Payment;

public class PendingPaymentDto
{
    public int? ReservationId { get; init; }

    public int? EnrollmentId { get; init; }

    public string Description { get; init; } = string.Empty;

    public DateTime Date { get; init; }

    public string? ClinicName { get; init; }

    public decimal AmountDue { get; init; }
}