using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Payment;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Domain.Entities.Payment;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Interfaces.Repositories;

namespace SafyaClinic.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _uow;

    public PaymentService(IUnitOfWork uow) => _uow = uow;

    public async Task<ServiceResult<PaymentDto>> CollectPaymentAsync(CollectPaymentRequest request, int currentUserId)
    {
        if (!await _uow.Patients.ExistsAsync(request.PatientId))
            return ServiceResult<PaymentDto>.Failure("Patient not found.");
        if (!Enum.TryParse<PaymentMethodEnum>(request.PaymentMethod, out var method))
            return ServiceResult<PaymentDto>.Failure(
                "Invalid payment method. Valid: Cash, CreditCard, BankTransfer, Insurance, MobilePayment.");
        if (request.Amount <= 0)
            return ServiceResult<PaymentDto>.Failure("Amount must be greater than zero.");

        var payment = new Payment
        {
            PatientId = request.PatientId,
            ReservationId = request.ReservationId,
            EnrollmentId = request.EnrollmentId,
            CollectedBy = 1,
            Amount = request.Amount,
            PaymentMethod = method,
            PaymentDate = DateTime.UtcNow,
            ReferenceNumber = request.ReferenceNumber?.Trim(),
            Notes = request.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Payments.AddAsync(payment);

        // Auto-mark reservation as paid if linked and amount covers total
        if (request.ReservationId.HasValue)
        {
            var reservation = await _uow.Reservations.GetByIdAsync(request.ReservationId.Value);
            if (reservation is not null)
            {
                reservation.IsPaid = true;
                reservation.UpdatedAt = DateTime.UtcNow;
                _uow.Reservations.Update(reservation);
            }
        }

        // Track total paid on enrollment if linked
        if (request.EnrollmentId.HasValue)
        {
            var enrollment = await _uow.NutritionEnrollments.GetByIdAsync(request.EnrollmentId.Value);
            if (enrollment is not null)
            {
                enrollment.TotalPaid += request.Amount;
                _uow.NutritionEnrollments.Update(enrollment);
            }
        }

        await _uow.SaveChangesAsync();
        return ServiceResult<PaymentDto>.Success(await BuildPaymentDtoAsync(payment));
    }

    public async Task<ServiceResult<PaymentDto>> GetPaymentByIdAsync(int paymentId)
    {
        var payment = await _uow.Payments.GetByIdAsync(paymentId);
        if (payment is null) return ServiceResult<PaymentDto>.Failure("Payment not found.");
        return ServiceResult<PaymentDto>.Success(await BuildPaymentDtoAsync(payment));
    }

    public async Task<ServiceResult<IEnumerable<PaymentDto>>> GetPatientPaymentsAsync(int patientId)
    {
        var payments = await _uow.Payments.FindAsync(p => p.PatientId == patientId);
        var dtos = new List<PaymentDto>();
        foreach (var p in payments.OrderByDescending(p => p.PaymentDate))
            dtos.Add(await BuildPaymentDtoAsync(p));
        return ServiceResult<IEnumerable<PaymentDto>>.Success(dtos);
    }

    public async Task<ServiceResult<PatientFinancialSummaryDto>> GetPatientFinancialSummaryAsync(
        int patientId)
    {
        var patient = await _uow.Patients.GetByIdAsync(patientId);
        if (patient is null) return ServiceResult<PatientFinancialSummaryDto>.Failure("Patient not found.");

        var payments = await _uow.Payments.FindAsync(p => p.PatientId == patientId);
        var reservations = await _uow.Reservations.FindAsync(r => r.PatientId == patientId);
        var enrollments = await _uow.NutritionEnrollments.FindAsync(e => e.PatientId == patientId);

        var totalCharged = reservations.Sum(r => r.TotalAmount ?? 0m)
                         + enrollments.Sum(e => e.FinalPrice);
        var totalPaid = payments.Sum(p => p.Amount);

        var dtos = new List<PaymentDto>();
        foreach (var p in payments.OrderByDescending(p => p.PaymentDate))
            dtos.Add(await BuildPaymentDtoAsync(p));

        return ServiceResult<PatientFinancialSummaryDto>.Success(new PatientFinancialSummaryDto
        {
            PatientId = patientId,
            PatientName = $"{patient.FirstName} {patient.LastName}",
            TotalCharged = totalCharged,
            TotalPaid = totalPaid,
            Payments = dtos
        });
    }

    public async Task<ServiceResult<IEnumerable<PaymentDto>>> GetPaymentsByDateRangeAsync(
        DateTime from, DateTime to)
    {
        var payments = await _uow.Payments.FindAsync(
            p => p.PaymentDate >= from && p.PaymentDate <= to);
        var dtos = new List<PaymentDto>();
        foreach (var p in payments.OrderByDescending(p => p.PaymentDate))
            dtos.Add(await BuildPaymentDtoAsync(p));
        return ServiceResult<IEnumerable<PaymentDto>>.Success(dtos);
    }

    // ── Mapper ────────────────────────────────────────────────

    private async Task<PaymentDto> BuildPaymentDtoAsync(Payment p)
    {
        var patient = await _uow.Patients.GetByIdAsync(p.PatientId);
        var collector = await _uow.Users.GetByIdAsync(p.CollectedBy);

        return new PaymentDto
        {
            Id = p.Id,
            PatientId = p.PatientId,
            PatientName = patient is null ? "" : $"{patient.FirstName} {patient.LastName}",
            ReservationId = p.ReservationId,
            EnrollmentId = p.EnrollmentId,
            CollectorName = collector?.FullName ?? "",
            Amount = p.Amount,
            PaymentMethod = p.PaymentMethod.ToString(),
            PaymentDate = p.PaymentDate,
            ReferenceNumber = p.ReferenceNumber,
            Notes = p.Notes
        };
    }
}