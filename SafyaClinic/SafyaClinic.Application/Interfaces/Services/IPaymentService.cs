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
}