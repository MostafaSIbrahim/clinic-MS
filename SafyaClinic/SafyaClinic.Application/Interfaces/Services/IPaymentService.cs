using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Payment;

namespace SafyaClinic.Application.Interfaces.Services;

public interface IPaymentService
{
    Task<ServiceResult<PaymentDto>> CollectPaymentAsync(CollectPaymentRequest request, int currentUserId);
    Task<ServiceResult<PaymentDto>> GetPaymentByIdAsync(int paymentId);
    Task<ServiceResult<IEnumerable<PaymentDto>>> GetPatientPaymentsAsync(int patientId);
    Task<ServiceResult<PatientFinancialSummaryDto>> GetPatientFinancialSummaryAsync(int patientId);
    // Payment access: UTC bounds with an exclusive end; optional assigned-doctor scope.
    Task<ServiceResult<IEnumerable<PaymentDto>>> GetPaymentsByDateRangeAsync(DateTime from, DateTime toExclusive, int? doctorId = null);

    // ── New payment operations ─────────────────────────────────
    Task<ServiceResult<PaymentDto>> CancelPaymentAsync(CancelPaymentRequest request, int currentUserId);

    /// <summary>P6b: write off up to a visit's remaining balance, with a reason. Covers the visit; never revenue.</summary>
    Task<ServiceResult<PaymentDto>> WriteOffAsync(WriteOffRequest request, int currentUserId);

    /// <summary>P6b: undo a write-off (it becomes a void); the amount is due again.</summary>
    Task<ServiceResult<PaymentDto>> CancelWriteOffAsync(CancelWriteOffRequest request, int currentUserId);
    Task<ServiceResult<PaymentDto>> ChangePaymentAmountAsync(ChangePaymentAmountRequest request, int currentUserId);

    // ── Dashboard ────────────────────────────────────────────────
    // Payment access: unpaidOnly skips all revenue and breakdown queries.
    /// <summary>
    /// M2b: with <paramref name="pageSize"/>, the unpaid lists hold one page each (newest first); their totals and
    /// counts still cover every unpaid visit. Without it, the lists are complete.
    /// </summary>
    Task<ServiceResult<PaymentDashboardDto>> GetPaymentDashboardAsync(DateTime? from = null, DateTime? to = null, bool unpaidOnly = false,
        int? pageSize = null, int completedPage = 1, int pendingPage = 1);

    // ── Collect-form helper ─────────────────────────────────────
    /// <summary>
    /// Computes the outstanding due amount to pre-fill on the Collect Payment form:
    /// reservation balance if reservationId is given, enrollment balance if enrollmentId is
    /// given, otherwise the patient's overall outstanding balance.
    /// </summary>
    Task<ServiceResult<decimal>> GetDueAmountAsync(int patientId, int? reservationId, int? enrollmentId);



    // ── Dashboard drill-down ──────────────────────────────────────
    /// <summary>
    /// Builds the per-payment detail report for a single line clicked on the
    /// "Amount by Clinic" or "Amount by Patient Source" dashboard tables. groupType is
    /// "clinic" or "source"; groupId is the ClinicId/PatientSourceId of the clicked row,
    /// or null for the "No Clinic"/"No Source" row. from/to are optional — omitting
    /// either leaves that side unbounded, matching GetPaymentDashboardAsync's own
    /// optional date filtering so a drill-down opened from an unfiltered dashboard
    /// defaults to the same unfiltered range that produced the number being drilled into.
    /// </summary>
    Task<ServiceResult<PaymentLineDetailReportDto>> GetDashboardLineDetailsAsync(
        string groupType, int? groupId, DateTime? from, DateTime? to);
    /// <summary>Total of active payments with fromUtc &lt;= PaymentDate &lt; toUtcExclusive (UTC instants).</summary>
    Task<ServiceResult<decimal>> GetRevenueBetweenAsync(DateTime fromUtc, DateTime toUtcExclusive);
    // ── Payment audit trail ───────────────────────────────────────
    Task<ServiceResult<PaymentAuditDto>> GetPaymentAuditAsync(
    int paymentId,
    PaginationRequest pagination);
}
