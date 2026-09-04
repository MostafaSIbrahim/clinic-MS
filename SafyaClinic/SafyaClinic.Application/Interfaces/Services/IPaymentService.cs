using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Payment;

namespace SafyaClinic.Application.Interfaces.Services;

public interface IPaymentService
{
    Task<ServiceResult<PaymentDto>> CollectPaymentAsync(CollectPaymentRequest request, int currentUserId);
    Task<ServiceResult<PaymentDto>> GetPaymentByIdAsync(int paymentId);
    Task<ServiceResult<IEnumerable<PaymentDto>>> GetPatientPaymentsAsync(int patientId);
    Task<ServiceResult<PatientFinancialSummaryDto>> GetPatientFinancialSummaryAsync(int patientId);
    Task<ServiceResult<IEnumerable<PaymentDto>>> GetPaymentsByDateRangeAsync(DateTime from, DateTime to);

    // ── New payment operations ─────────────────────────────────
    Task<ServiceResult<PaymentDto>> CancelPaymentAsync(CancelPaymentRequest request, int currentUserId);
    Task<ServiceResult<PaymentDto>> ChangePaymentAmountAsync(ChangePaymentAmountRequest request, int currentUserId);

    // ── Dashboard ────────────────────────────────────────────────
    Task<ServiceResult<PaymentDashboardDto>> GetPaymentDashboardAsync(DateTime? from = null, DateTime? to = null);

    // ── Collect-form helper ─────────────────────────────────────
    /// <summary>
    /// Computes the outstanding due amount to pre-fill on the Collect Payment form:
    /// reservation balance if reservationId is given, enrollment balance if enrollmentId is
    /// given, otherwise the patient's overall outstanding balance.
    /// </summary>
    Task<ServiceResult<decimal>> GetDueAmountAsync(int patientId, int? reservationId, int? enrollmentId);

    // ── One-time data backfill ───────────────────────────────────
    /// <summary>
    /// Recomputes Reservation.IsPaid for every reservation that has a linked payment,
    /// using the same coverage rule as RecalculateReservationPaidStatusAsync (Active +
    /// Cancelled payments vs TotalAmount). Intended as a one-off maintenance action to
    /// correct historical rows written under the pre-fix save-ordering bug, where IsPaid
    /// could be computed against a stale/incomplete payment total. Safe to run multiple
    /// times — it is idempotent.
    /// </summary>
    /// <returns>The number of reservations whose IsPaid value actually changed.</returns>
    Task<ServiceResult<int>> RecalculateAllReservationsPaidStatusAsync();

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
}
