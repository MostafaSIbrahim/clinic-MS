using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Billing;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Payment;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Interfaces.Repositories;

namespace SafyaClinic.Tests.Application;

/// <summary>
/// M2a: the payment dashboard exactly as it was before M2 (loaded every reservation into memory and added up in
/// code), kept only to prove the database version gives the same figures. Shared with tests/PerformanceChecks.
/// Do not change this file.
/// </summary>
public static class LegacyPaymentDashboard
{
    public static async Task<ServiceResult<PaymentDashboardDto>> ComputeAsync(IUnitOfWork _uow, DateTime? from = null, DateTime? to = null, bool unpaidOnly = false)
    {
        var (fromInclusive, toInclusive) = NormalizeDateRange(from, to);

        var reservationsQuery = _uow.Reservations.Query();
        // Payment access: reception only needs reservations whose queue can have a collectible balance.
        if (unpaidOnly)
            reservationsQuery = reservationsQuery.Where(r =>
                r.Status.StatusName != "Cancelled" && r.Status.StatusName != "NoShow" &&
                (r.QueueStatus == PatientQueueStatus.InConsultation || r.QueueStatus == PatientQueueStatus.Finished));

        var paymentsQuery = _uow.Payments.Query();

        if (fromInclusive.HasValue)
        {
            paymentsQuery = paymentsQuery
                .Where(p => p.PaymentDate >= fromInclusive.Value);
        }

        if (toInclusive.HasValue)
        {
            paymentsQuery = paymentsQuery
                .Where(p => p.PaymentDate <= toInclusive.Value);
        }



        var reservations = await reservationsQuery
            .Select(r => new
            {
                r.Id,
                r.PatientId,
                PatientName = $"{r.Patient.FirstName} {r.Patient.LastName}",
                DoctorName = r.Doctor.FullName,
                ClinicName = r.Clinic.Name,
                StatusName = r.Status.StatusName,
                r.ReservationDate,
                r.TotalAmount,
                r.QueueStatus,

                Paid = r.Payments
                    .Where(p => p.Status == PaymentStatusEnum.Active)
                    .Sum(p => (decimal?)p.Amount) ?? 0m,

                WrittenOff = r.Payments
                    .Where(p => p.Status == PaymentStatusEnum.WrittenOff)
                    .Sum(p => (decimal?)p.Amount) ?? 0m
            })
            .ToListAsync();

        var unpaidCompleted = new List<UnpaidReservationDto>();
        var unpaidPending = new List<UnpaidReservationDto>();

        foreach (var r in reservations)
        {
            if (r.StatusName is "Cancelled" or "NoShow")
                continue;

            if (r.QueueStatus != PatientQueueStatus.InConsultation &&
                r.QueueStatus != PatientQueueStatus.Finished)
                continue;

            // P6: the same rule as everywhere else (Paid = Active, WrittenOff = WrittenOff).
            if (Coverage.Due(r.TotalAmount, r.Paid + r.WrittenOff) <= 0m)
                continue;

            var dto = new UnpaidReservationDto
            {
                ReservationId = r.Id,
                PatientId = r.PatientId,
                PatientName = r.PatientName,
                DoctorName = r.DoctorName,
                ClinicName = r.ClinicName,
                ReservationDate = r.ReservationDate,
                StatusName = r.StatusName,
                TotalAmount = r.TotalAmount,
                AmountPaid = r.Paid,
                WrittenOff = r.WrittenOff
            };

            if (r.QueueStatus == PatientQueueStatus.Finished)
                unpaidCompleted.Add(dto);
            else
                unpaidPending.Add(dto);
        }

        // Payment access: stop before querying payment totals, sources, or clinic breakdowns.
        if (unpaidOnly)
            return ServiceResult<PaymentDashboardDto>.Success(new PaymentDashboardDto
            {
                UnpaidCompletedReservations = unpaidCompleted,
                UnpaidPendingReservations = unpaidPending,
                TotalUnpaidCompleted = unpaidCompleted.Sum(r => r.Balance),
                TotalUnpaidPending = unpaidPending.Sum(r => r.Balance)
            });

        var payments = await paymentsQuery
            .Select(p => new
            {
                p.Id,
                p.ReservationId,
                p.Amount,
                p.ClinicId,
                p.PatientSourceId,
                p.SourceDeductionAmount,
                p.ClinicNetAmount,
                p.Status,
                p.WriteOffReason
            })
            .ToListAsync();

        var activePayments = payments
            .Where(p => p.Status == PaymentStatusEnum.Active)
            .ToList();

        // P6b: write-offs in the period, by reason; shown separately, never as collected money.
        var writeOffs = payments.Where(p => p.Status == PaymentStatusEnum.WrittenOff).ToList();
        var writeOffsByReason = writeOffs
            .GroupBy(p => p.WriteOffReason?.ToString() ?? "Before P6 (cancelled payment)")
            .Select(g => new WriteOffReasonAmountDto(g.Key, g.Count(), g.Sum(p => p.Amount)))
            .OrderByDescending(r => r.Amount)
            .ToList();

        var reservationsDict = reservations.ToDictionary(r => r.Id, r => r);

        var fullyPaidPayments = activePayments
                .Where(p =>
                    !p.ReservationId.HasValue ||
                    (
                        reservationsDict.TryGetValue(
                            p.ReservationId.Value, out var reservation) &&
                        reservation.Paid + reservation.WrittenOff >=
                            (reservation.TotalAmount ?? 0m)
                    ))
                .ToList();

        // ── Amount by source ─────────────────────────────────────
        var sourceIds = activePayments
             .Where(p => p.PatientSourceId.HasValue)
             .Select(p => p.PatientSourceId!.Value)
             .Distinct()
             .ToList();

                var sourceNames = await _uow.PatientSources.Query()
                    .Where(s => sourceIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.Name);

                var bySource = new List<SourceAmountDto>();

                foreach (var grp in activePayments.GroupBy(p => p.PatientSourceId))
                {
                    string name = "No Source";

                    if (grp.Key.HasValue)
                    {
                        name = sourceNames.TryGetValue(grp.Key.Value, out var sourceName)
                            ? sourceName
                            : "Unknown Source";
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
        var clinicIds = activePayments
                .Where(p => p.ClinicId.HasValue)
                .Select(p => p.ClinicId!.Value)
                .Distinct()
                .ToList();

                var clinicNames = await _uow.Clinics.Query()
                    .Where(c => clinicIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => c.Name);

                var byClinic = new List<ClinicAmountDto>();

                foreach (var grp in activePayments.GroupBy(p => p.ClinicId))
                {
                    string name = "No Clinic";

                    if (grp.Key.HasValue)
                    {
                        name = clinicNames.TryGetValue(grp.Key.Value, out var clinicName)
                            ? clinicName
                            : "Unknown Clinic";
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
            FullyPaidPaymentCount = fullyPaidPayments.Count,
            TotalUnpaidCompleted = unpaidCompleted.Sum(r => r.Balance),
            TotalUnpaidPending = unpaidPending.Sum(r => r.Balance),
            TotalFullyPaid = fullyPaidPayments.Sum(p => p.Amount),
            AmountBySource = bySource.OrderByDescending(s => s.TotalCollected),
            AmountByClinic = byClinic.OrderByDescending(c => c.TotalCollected),
            TotalWrittenOff = writeOffs.Sum(p => p.Amount),
            WrittenOffByReason = writeOffsByReason
        });
    }

    private static (DateTime? fromInclusive, DateTime? toInclusive) NormalizeDateRange(DateTime? from, DateTime? to) =>
        (from?.Date, to?.Date.AddDays(1).AddTicks(-1));
}
