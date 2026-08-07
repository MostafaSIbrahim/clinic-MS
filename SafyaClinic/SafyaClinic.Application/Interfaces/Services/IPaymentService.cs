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
}
