namespace SafyaClinic.Application.DTOs.Payment;

public class PaymentDto
{
    /// <summary>P6b: the write-off reason category, for written-off payments.</summary>
    public string? WriteOffReason { get; init; }
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

/// <summary>P6b: write off (part of) a visit's remaining balance.</summary>
public class WriteOffRequest
{
    public int PatientId { get; init; }
    public int ReservationId { get; init; }
    public decimal Amount { get; init; }
    /// <summary>Discount, Hardship, StaffOrFamily, BillingError or Other.</summary>
    public string Reason { get; init; } = string.Empty;
    /// <summary>Required when the reason is Other.</summary>
    public string? Note { get; init; }
}

/// <summary>P6b: undo a write-off; the balance becomes due again.</summary>
public class CancelWriteOffRequest
{
    public int PaymentId { get; init; }
    public string Reason { get; init; } = string.Empty;
}

/// <summary>P6b: write-offs in the dashboard period, by reason.</summary>
public sealed record WriteOffReasonAmountDto(string Reason, int Count, decimal Amount);

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
    public IReadOnlyList<PendingPaymentDto> PendingPayments { get; init; }
    = Array.Empty<PendingPaymentDto>();

    public decimal CollectibleBalance =>
        PendingPayments.Sum(p => p.AmountDue);

    public decimal UnallocatedCoverage { get; init; }
}

// ── Payment Dashboard ───────────────────────────────────────────

public class PaymentDashboardDto
{
    public IEnumerable<UnpaidReservationDto> UnpaidCompletedReservations { get; init; } = Enumerable.Empty<UnpaidReservationDto>();
    public IEnumerable<UnpaidReservationDto> UnpaidPendingReservations { get; init; } = Enumerable.Empty<UnpaidReservationDto>();
    /// <summary>M2b: how many unpaid visits there are in each list (the lists may hold one page only).</summary>
    public int UnpaidCompletedCount { get; init; }
    public int UnpaidPendingCount { get; init; }
    /// <summary>M2b: the page shown of each list, and its size (0 = complete lists).</summary>
    public int CompletedPage { get; init; } = 1;
    public int PendingPage { get; init; } = 1;
    public int PageSize { get; init; }
    public int FullyPaidPaymentCount { get; init; }

    public decimal TotalUnpaidCompleted { get; init; }
    public decimal TotalUnpaidPending { get; init; }
    public decimal TotalFullyPaid { get; init; }

    public IEnumerable<SourceAmountDto> AmountBySource { get; init; } = Enumerable.Empty<SourceAmountDto>();
    public IEnumerable<ClinicAmountDto> AmountByClinic { get; init; } = Enumerable.Empty<ClinicAmountDto>();

    /// <summary>P6b: write-offs in the period. Never part of the collected totals above.</summary>
    public decimal TotalWrittenOff { get; init; }
    public IEnumerable<WriteOffReasonAmountDto> WrittenOffByReason { get; init; } = Enumerable.Empty<WriteOffReasonAmountDto>();
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

    /// <summary>Sum of written-off payments for this reservation (P6: status WrittenOff), not owed again.</summary>
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

// ── Dashboard drill-down (per-line detail popup) ──────────────────

/// <summary>
/// One row in the drill-down report shown when a user clicks a line in the
/// "Amount by Clinic" / "Amount by Patient Source" dashboard tables.
/// </summary>
public class PaymentLineDetailDto
{
    public int PaymentId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public DateTime PaymentDate { get; init; }
    public decimal AmountPaid { get; init; }

    /// <summary>
    /// The deducted amount for this line: source deduction when drilling into a Patient
    /// Source group, clinic's cut (Amount - ClinicNetAmount) when drilling into a Clinic
    /// group.
    /// </summary>
    public decimal DeductedAmount { get; init; }

    public string ClinicName { get; init; } = string.Empty;
    public string PatientSourceName { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string? ReferenceNumber { get; init; }
}

public class PaymentLineDetailReportDto
{
    public string GroupLabel { get; init; } = string.Empty;

    /// <summary>Null means unbounded (no start filter) — mirrors the dashboard's optional From filter.</summary>
    public DateTime? From { get; init; }

    /// <summary>Null means unbounded (no end filter) — mirrors the dashboard's optional To filter.</summary>
    public DateTime? To { get; init; }
    public IEnumerable<PaymentLineDetailDto> Lines { get; init; } = Enumerable.Empty<PaymentLineDetailDto>();
    public decimal TotalAmountPaid => Lines.Sum(l => l.AmountPaid);
    public decimal TotalDeducted => Lines.Sum(l => l.DeductedAmount);

    // ── Diagnostics for the "no rows in this range" case ────────
    /// <summary>
    /// Count of Active payments for this group across ALL time, ignoring From/To.
    /// Lets the UI explain "this group has payments, just not in the range you picked"
    /// instead of leaving an unexplained empty table.
    /// </summary>
    public int TotalPaymentsAllTime { get; init; }
    public DateTime? EarliestPaymentDate { get; init; }
    public DateTime? LatestPaymentDate { get; init; }
}
