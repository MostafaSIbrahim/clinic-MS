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

    // Clinic / source attribution
    public int? ClinicId { get; init; }
    public string? ClinicName { get; init; }
    public int? PatientSourceId { get; init; }
    public string? PatientSourceName { get; init; }
    public bool IsFirstVisitDeduction { get; init; }
    public decimal? DeductionPercentage { get; init; }
    public decimal SourceDeductionAmount { get; init; }
    public decimal ClinicNetAmount { get; init; }

    // Status
    public string Status { get; init; } = "Active";
    public DateTime? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }
    public decimal? OriginalAmount { get; init; }
    public DateTime? LastModifiedAt { get; init; }
}

public class CollectPaymentRequest
{
    public int PatientId { get; init; }
    public int? ReservationId { get; init; }
    public int? EnrollmentId { get; init; }
    public int ClinicId { get; init; }
    public int CollectedBy { get; init; }
    public decimal Amount { get; init; }
    public string PaymentMethod { get; init; } = "Cash";
    public string? ReferenceNumber { get; init; }
    public string? Notes { get; init; }
}

public class CancelPaymentRequest
{
    public int PaymentId { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public class ChangePaymentAmountRequest
{
    public int PaymentId { get; init; }
    public decimal NewAmount { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public class PatientFinancialSummaryDto
{
    public int PatientId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public decimal TotalCharged { get; init; }
    public decimal TotalPaid { get; init; }

    /// <summary>
    /// Sum of cancelled payments. Once a payment is cancelled, the amount is written off
    /// and does NOT reopen as due — it is excluded from both "paid" and "balance".
    /// </summary>
    public decimal TotalWrittenOff { get; init; }
    public decimal Balance => Math.Max(0m, TotalCharged - TotalPaid - TotalWrittenOff);
    public IEnumerable<PaymentDto> Payments { get; init; } = Enumerable.Empty<PaymentDto>();
}

// ── Payment Dashboard ───────────────────────────────────────────

public class PaymentDashboardDto
{
    public IEnumerable<UnpaidReservationDto> UnpaidCompletedReservations { get; init; } = Enumerable.Empty<UnpaidReservationDto>();
    public IEnumerable<UnpaidReservationDto> UnpaidPendingReservations { get; init; } = Enumerable.Empty<UnpaidReservationDto>();
    public IEnumerable<PaymentDto> FullyPaidPayments { get; init; } = Enumerable.Empty<PaymentDto>();

    public decimal TotalUnpaidCompleted { get; init; }
    public decimal TotalUnpaidPending { get; init; }
    public decimal TotalFullyPaid { get; init; }

    public IEnumerable<SourceAmountDto> AmountBySource { get; init; } = Enumerable.Empty<SourceAmountDto>();
    public IEnumerable<ClinicAmountDto> AmountByClinic { get; init; } = Enumerable.Empty<ClinicAmountDto>();
}

public class UnpaidReservationDto
{
    public int ReservationId { get; init; }
    public int PatientId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public string DoctorName { get; init; } = string.Empty;
    public string ClinicName { get; init; } = string.Empty;
    public DateTime ReservationDate { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public decimal? TotalAmount { get; init; }
    public decimal AmountPaid { get; init; }

    /// <summary>Sum of cancelled payments for this reservation — written off, not owed again.</summary>
    public decimal WrittenOff { get; init; }
    public decimal Balance => Math.Max(0m, (TotalAmount ?? 0m) - AmountPaid - WrittenOff);
}

public class SourceAmountDto
{
    public int? PatientSourceId { get; init; }
    public string PatientSourceName { get; init; } = string.Empty;
    public decimal TotalCollected { get; init; }
    public decimal TotalSourceDeduction { get; init; }
    public int PaymentCount { get; init; }
}

public class ClinicAmountDto
{
    public int? ClinicId { get; init; }
    public string ClinicName { get; init; } = string.Empty;
    public decimal TotalCollected { get; init; }
    public decimal TotalClinicNet { get; init; }
    public int PaymentCount { get; init; }
}
