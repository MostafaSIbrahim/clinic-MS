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
        var patient = await _uow.Patients.GetByIdAsync(request.PatientId);
        if (patient is null)
            return ServiceResult<PaymentDto>.Failure("Patient not found.");
        if (!await _uow.Clinics.ExistsAsync(request.ClinicId))
            return ServiceResult<PaymentDto>.Failure("Clinic not found.");
        if (!Enum.TryParse<PaymentMethodEnum>(request.PaymentMethod, out var method))
            return ServiceResult<PaymentDto>.Failure(
                "Invalid payment method. Valid: Cash, CreditCard, BankTransfer, Insurance, MobilePayment.");
        if (request.Amount <= 0)
            return ServiceResult<PaymentDto>.Failure("Amount must be greater than zero.");

        // ── First-visit source/clinic deduction ─────────────────
        var priorActivePayments = await _uow.Payments.FindAsync(
            p => p.PatientId == request.PatientId && p.Status == PaymentStatusEnum.Active);
        var isFirstVisit = !priorActivePayments.Any();

        decimal deductionPercentage = 0m;
        decimal sourceDeduction = 0m;
        var applyDeduction = false;

        if (isFirstVisit && patient.PatientSourceId.HasValue)
        {
            var source = await _uow.PatientSources.GetByIdAsync(patient.PatientSourceId.Value);
            if (source is not null && source.IsActive)
            {
                var agreement = (await _uow.ClinicSourceAgreements.FindAsync(a =>
                        a.ClinicId == request.ClinicId &&
                        a.PatientSourceId == patient.PatientSourceId.Value &&
                        a.IsActive))
                    .FirstOrDefault();

                deductionPercentage = agreement?.DeductionPercentage ?? source.DefaultDeductionPercentage;
                if (deductionPercentage > 0)
                {
                    sourceDeduction = Math.Round(request.Amount * deductionPercentage / 100m, 2);
                    applyDeduction = true;
                }
            }
        }

        var payment = new Payment
        {
            PatientId = request.PatientId,
            ReservationId = request.ReservationId,
            EnrollmentId = request.EnrollmentId,
            ClinicId = request.ClinicId,
            PatientSourceId = patient.PatientSourceId,
            CollectedBy = currentUserId > 0 ? currentUserId : 1,
            Amount = request.Amount,
            OriginalAmount = request.Amount,
            PaymentMethod = method,
            PaymentDate = DateTime.UtcNow,
            ReferenceNumber = request.ReferenceNumber?.Trim(),
            Notes = request.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsFirstVisitDeduction = applyDeduction,
            DeductionPercentage = applyDeduction ? deductionPercentage : null,
            SourceDeductionAmount = sourceDeduction,
            ClinicNetAmount = request.Amount - sourceDeduction,
            Status = PaymentStatusEnum.Active
        };

        await _uow.Payments.AddAsync(payment);

        // Auto-mark reservation as paid if linked and payments cover total
        if (request.ReservationId.HasValue)
            await RecalculateReservationPaidStatusAsync(request.ReservationId.Value);

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

        var activePayments = payments.Where(p => p.Status == PaymentStatusEnum.Active).ToList();
        var cancelledPayments = payments.Where(p => p.Status == PaymentStatusEnum.Cancelled).ToList();

        var totalCharged = reservations.Sum(r => r.TotalAmount ?? 0m)
                         + enrollments.Sum(e => e.FinalPrice);
        var totalPaid = activePayments.Sum(p => p.Amount);
        var totalWrittenOff = cancelledPayments.Sum(p => p.Amount);

        var dtos = new List<PaymentDto>();
        foreach (var p in payments.OrderByDescending(p => p.PaymentDate))
            dtos.Add(await BuildPaymentDtoAsync(p));

        return ServiceResult<PatientFinancialSummaryDto>.Success(new PatientFinancialSummaryDto
        {
            PatientId = patientId,
            PatientName = $"{patient.FirstName} {patient.LastName}",
            TotalCharged = totalCharged,
            TotalPaid = totalPaid,
            TotalWrittenOff = totalWrittenOff,
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

    // ── Cancel payment ───────────────────────────────────────────

    public async Task<ServiceResult<PaymentDto>> CancelPaymentAsync(CancelPaymentRequest request, int currentUserId)
    {
        var payment = await _uow.Payments.GetByIdAsync(request.PaymentId);
        if (payment is null)
            return ServiceResult<PaymentDto>.Failure("Payment not found.");
        if (payment.Status == PaymentStatusEnum.Cancelled)
            return ServiceResult<PaymentDto>.Failure("Payment is already cancelled.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return ServiceResult<PaymentDto>.Failure("A cancellation reason is required.");

        var oldAmount = payment.Amount;

        payment.Status = PaymentStatusEnum.Cancelled;
        payment.CancelledAt = DateTime.UtcNow;
        payment.CancelledBy = currentUserId;
        payment.CancellationReason = request.Reason.Trim();
        payment.LastModifiedAt = DateTime.UtcNow;
        payment.LastModifiedBy = currentUserId;

        _uow.Payments.Update(payment);

        await _uow.PaymentAdjustments.AddAsync(new PaymentAdjustment
        {
            PaymentId = payment.Id,
            ActionType = "Cancelled",
            OldAmount = oldAmount,
            NewAmount = 0m,
            Reason = request.Reason.Trim(),
            PerformedBy = currentUserId,
            PerformedAt = DateTime.UtcNow
        });

        // NOTE: Cancelling a payment writes the amount off — it is removed from "collected
        // revenue" (excluded from Active-only totals/reports) but it does NOT reopen the
        // patient's/reservation's due balance. We deliberately do NOT reverse
        // enrollment.TotalPaid here, and RecalculateReservationPaidStatusAsync below treats
        // Active + Cancelled payments as combined "coverage" so IsPaid stays true.
        if (payment.ReservationId.HasValue)
            await RecalculateReservationPaidStatusAsync(payment.ReservationId.Value);

        await _uow.SaveChangesAsync();
        return ServiceResult<PaymentDto>.Success(await BuildPaymentDtoAsync(payment), "Payment cancelled.");
    }

    // ── Change payment amount ────────────────────────────────────

    public async Task<ServiceResult<PaymentDto>> ChangePaymentAmountAsync(ChangePaymentAmountRequest request, int currentUserId)
    {
        var payment = await _uow.Payments.GetByIdAsync(request.PaymentId);
        if (payment is null)
            return ServiceResult<PaymentDto>.Failure("Payment not found.");
        if (payment.Status == PaymentStatusEnum.Cancelled)
            return ServiceResult<PaymentDto>.Failure("Cannot change the amount of a cancelled payment.");
        if (request.NewAmount <= 0)
            return ServiceResult<PaymentDto>.Failure("Amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return ServiceResult<PaymentDto>.Failure("A reason for the amount change is required.");

        var oldAmount = payment.Amount;

        // Re-derive the source/clinic split using the previously applied percentage (if any)
        var pct = payment.DeductionPercentage ?? 0m;
        var newSourceDeduction = payment.IsFirstVisitDeduction
            ? Math.Round(request.NewAmount * pct / 100m, 2)
            : 0m;

        payment.Amount = request.NewAmount;
        payment.SourceDeductionAmount = newSourceDeduction;
        payment.ClinicNetAmount = request.NewAmount - newSourceDeduction;
        payment.LastModifiedAt = DateTime.UtcNow;
        payment.LastModifiedBy = currentUserId;
        payment.Notes = string.IsNullOrWhiteSpace(payment.Notes)
            ? $"Amount changed from {oldAmount:0.##} to {request.NewAmount:0.##}: {request.Reason.Trim()}"
            : $"{payment.Notes} | Amount changed from {oldAmount:0.##} to {request.NewAmount:0.##}: {request.Reason.Trim()}";

        _uow.Payments.Update(payment);

        await _uow.PaymentAdjustments.AddAsync(new PaymentAdjustment
        {
            PaymentId = payment.Id,
            ActionType = "AmountChanged",
            OldAmount = oldAmount,
            NewAmount = request.NewAmount,
            Reason = request.Reason.Trim(),
            PerformedBy = currentUserId,
            PerformedAt = DateTime.UtcNow
        });

        // Adjust enrollment total paid by the delta
        if (payment.EnrollmentId.HasValue)
        {
            var enrollment = await _uow.NutritionEnrollments.GetByIdAsync(payment.EnrollmentId.Value);
            if (enrollment is not null)
            {
                enrollment.TotalPaid += request.NewAmount - oldAmount;
                if (enrollment.TotalPaid < 0) enrollment.TotalPaid = 0;
                _uow.NutritionEnrollments.Update(enrollment);
            }
        }

        if (payment.ReservationId.HasValue)
            await RecalculateReservationPaidStatusAsync(payment.ReservationId.Value);

        await _uow.SaveChangesAsync();
        return ServiceResult<PaymentDto>.Success(await BuildPaymentDtoAsync(payment), "Payment amount updated.");
    }

    // ── Payment dashboard ─────────────────────────────────────────

    public async Task<ServiceResult<PaymentDashboardDto>> GetPaymentDashboardAsync(DateTime? from = null, DateTime? to = null)
    {
        var reservations = (await _uow.Reservations.GetAllAsync()).ToList();
        var allPayments = (await _uow.Payments.GetAllAsync()).ToList();

        if (from.HasValue)
            allPayments = allPayments.Where(p => p.PaymentDate >= from.Value).ToList();
        if (to.HasValue)
            allPayments = allPayments.Where(p => p.PaymentDate <= to.Value).ToList();

        var activePayments = allPayments.Where(p => p.Status == PaymentStatusEnum.Active).ToList();
        var cancelledPayments = allPayments.Where(p => p.Status == PaymentStatusEnum.Cancelled).ToList();

        var paidByReservation = activePayments
            .Where(p => p.ReservationId.HasValue)
            .GroupBy(p => p.ReservationId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        // Cancelled amounts are written off — they still count as "covered" so a cancelled
        // payment does not reopen the reservation's due balance.
        var writtenOffByReservation = cancelledPayments
            .Where(p => p.ReservationId.HasValue)
            .GroupBy(p => p.ReservationId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        var unpaidCompleted = new List<UnpaidReservationDto>();
        var unpaidPending = new List<UnpaidReservationDto>();

        foreach (var r in reservations)
        {
            var statusEntity = await _uow.ReservationStatuses.GetByIdAsync(r.StatusId);
            var statusName = statusEntity?.StatusName ?? "";
            if (statusName is "Cancelled" or "NoShow") continue;

            var paid = paidByReservation.TryGetValue(r.Id, out var amt) ? amt : 0m;
            var writtenOff = writtenOffByReservation.TryGetValue(r.Id, out var wo) ? wo : 0m;
            var total = r.TotalAmount ?? 0m;
            if (paid + writtenOff >= total && total > 0) continue; // fully covered, skip

            var patient = await _uow.Patients.GetByIdAsync(r.PatientId);
            var doctor = await _uow.Users.GetByIdAsync(r.DoctorId);
            var clinic = await _uow.Clinics.GetByIdAsync(r.ClinicId);

            var dto = new UnpaidReservationDto
            {
                ReservationId = r.Id,
                PatientId = r.PatientId,
                PatientName = patient is null ? "" : $"{patient.FirstName} {patient.LastName}",
                DoctorName = doctor?.FullName ?? "",
                ClinicName = clinic?.Name ?? "",
                ReservationDate = r.ReservationDate,
                StatusName = statusName,
                TotalAmount = r.TotalAmount,
                AmountPaid = paid,
                WrittenOff = writtenOff
            };

            if (statusName == "Completed") unpaidCompleted.Add(dto);
            else unpaidPending.Add(dto);
        }

        var fullyPaidPayments = new List<PaymentDto>();
        foreach (var p in activePayments.Where(p => !p.ReservationId.HasValue ||
                    (paidByReservation.TryGetValue(p.ReservationId!.Value, out var paidAmt) &&
                     paidAmt + (writtenOffByReservation.TryGetValue(p.ReservationId!.Value, out var woAmt) ? woAmt : 0m) >=
                        (reservations.FirstOrDefault(r => r.Id == p.ReservationId)?.TotalAmount ?? 0m) &&
                     (reservations.FirstOrDefault(r => r.Id == p.ReservationId)?.TotalAmount ?? 0m) > 0)))
        {
            fullyPaidPayments.Add(await BuildPaymentDtoAsync(p));
        }

        // ── Amount by source ─────────────────────────────────────
        var bySource = new List<SourceAmountDto>();
        foreach (var grp in activePayments.GroupBy(p => p.PatientSourceId))
        {
            string name = "No Source";
            if (grp.Key.HasValue)
            {
                var s = await _uow.PatientSources.GetByIdAsync(grp.Key.Value);
                name = s?.Name ?? "Unknown Source";
            }
            bySource.Add(new SourceAmountDto
            {
                PatientSourceId = grp.Key,
                PatientSourceName = name,
                TotalCollected = grp.Sum(p => p.Amount),
                TotalSourceDeduction = grp.Sum(p => p.SourceDeductionAmount),
                PaymentCount = grp.Count()
            });
        }

        // ── Amount by clinic ─────────────────────────────────────
        var byClinic = new List<ClinicAmountDto>();
        foreach (var grp in activePayments.GroupBy(p => p.ClinicId))
        {
            string name = "No Clinic";
            if (grp.Key.HasValue)
            {
                var c = await _uow.Clinics.GetByIdAsync(grp.Key.Value);
                name = c?.Name ?? "Unknown Clinic";
            }
            byClinic.Add(new ClinicAmountDto
            {
                ClinicId = grp.Key,
                ClinicName = name,
                TotalCollected = grp.Sum(p => p.Amount),
                TotalClinicNet = grp.Sum(p => p.ClinicNetAmount),
                PaymentCount = grp.Count()
            });
        }

        return ServiceResult<PaymentDashboardDto>.Success(new PaymentDashboardDto
        {
            UnpaidCompletedReservations = unpaidCompleted.OrderByDescending(r => r.ReservationDate),
            UnpaidPendingReservations = unpaidPending.OrderByDescending(r => r.ReservationDate),
            FullyPaidPayments = fullyPaidPayments.OrderByDescending(p => p.PaymentDate),
            TotalUnpaidCompleted = unpaidCompleted.Sum(r => r.Balance),
            TotalUnpaidPending = unpaidPending.Sum(r => r.Balance),
            TotalFullyPaid = fullyPaidPayments.Sum(p => p.Amount),
            AmountBySource = bySource.OrderByDescending(s => s.TotalCollected),
            AmountByClinic = byClinic.OrderByDescending(c => c.TotalCollected)
        });
    }

    // ── Collect-form helper ─────────────────────────────────────

    public async Task<ServiceResult<decimal>> GetDueAmountAsync(int patientId, int? reservationId, int? enrollmentId)
    {
        if (!await _uow.Patients.ExistsAsync(patientId))
            return ServiceResult<decimal>.Failure("Patient not found.");

        if (reservationId.HasValue)
        {
            var reservation = await _uow.Reservations.GetByIdAsync(reservationId.Value);
            if (reservation is null) return ServiceResult<decimal>.Failure("Reservation not found.");

            var payments = await _uow.Payments.FindAsync(
                p => p.ReservationId == reservationId.Value &&
                     (p.Status == PaymentStatusEnum.Active || p.Status == PaymentStatusEnum.Cancelled));
            var covered = payments.Sum(p => p.Amount);
            var due = Math.Max(0m, (reservation.TotalAmount ?? 0m) - covered);
            return ServiceResult<decimal>.Success(due);
        }

        if (enrollmentId.HasValue)
        {
            var enrollment = await _uow.NutritionEnrollments.GetByIdAsync(enrollmentId.Value);
            if (enrollment is null) return ServiceResult<decimal>.Failure("Enrollment not found.");

            var due = Math.Max(0m, enrollment.FinalPrice - enrollment.TotalPaid);
            return ServiceResult<decimal>.Success(due);
        }

        // No specific reservation/enrollment — overall outstanding balance for the patient.
        var summary = await GetPatientFinancialSummaryAsync(patientId);
        return summary.IsSuccess
            ? ServiceResult<decimal>.Success(summary.Data!.Balance)
            : ServiceResult<decimal>.Success(0m);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task RecalculateReservationPaidStatusAsync(int reservationId)
    {
        var reservation = await _uow.Reservations.GetByIdAsync(reservationId);
        if (reservation is null) return;

        // "Coverage" = Active (real cash) + Cancelled (written off, but not owed again).
        // This intentionally does NOT re-open the due balance when a payment is cancelled.
        var payments = await _uow.Payments.FindAsync(
            p => p.ReservationId == reservationId &&
                 (p.Status == PaymentStatusEnum.Active || p.Status == PaymentStatusEnum.Cancelled));
        var totalCoverage = payments.Sum(p => p.Amount);

        reservation.IsPaid = reservation.TotalAmount.HasValue &&
                              totalCoverage >= reservation.TotalAmount.Value &&
                              reservation.TotalAmount.Value > 0;
        reservation.UpdatedAt = DateTime.UtcNow;
        _uow.Reservations.Update(reservation);
    }

    // ── Mapper ────────────────────────────────────────────────

    private async Task<PaymentDto> BuildPaymentDtoAsync(Payment p)
    {
        var patient = await _uow.Patients.GetByIdAsync(p.PatientId);
        var collector = await _uow.Users.GetByIdAsync(p.CollectedBy);
        var clinic = p.ClinicId.HasValue ? await _uow.Clinics.GetByIdAsync(p.ClinicId.Value) : null;
        var source = p.PatientSourceId.HasValue ? await _uow.PatientSources.GetByIdAsync(p.PatientSourceId.Value) : null;

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
            Notes = p.Notes,
            ClinicId = p.ClinicId,
            ClinicName = clinic?.Name,
            PatientSourceId = p.PatientSourceId,
            PatientSourceName = source?.Name,
            IsFirstVisitDeduction = p.IsFirstVisitDeduction,
            DeductionPercentage = p.DeductionPercentage,
            SourceDeductionAmount = p.SourceDeductionAmount,
            ClinicNetAmount = p.ClinicNetAmount,
            Status = p.Status.ToString(),
            CancelledAt = p.CancelledAt,
            CancellationReason = p.CancellationReason,
            OriginalAmount = p.OriginalAmount,
            LastModifiedAt = p.LastModifiedAt
        };
    }
}
