using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.DTOs.Payment;
using SafyaClinic.Domain.Entities.Patient;
using SafyaClinic.Domain.Entities.Payment;
using SafyaClinic.Domain.Entities.Reservation;
using SafyaClinic.Domain.Entities.Settings;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Infrastructure.Data;

namespace SafyaClinic.Tests.Application;

/// <summary>
/// M2a: a varied, repeatable set of visits and payments for the payment dashboard: every status and arrival
/// state, prices of 800, 200, 0 and none, partial and full payments, write-offs with and without a reason (before
/// P6), voided payments, payments with no visit, clinic or source, spread over about a year. Shared with
/// tests/PerformanceChecks, which uses it with 50,000 reservations. Expects a database seeded by DbSeeder.
/// </summary>
public static class DashboardTestData
{
    public static readonly DateTime Start = new(2025, 11, 1, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>The dashboard's figures in a stable order, so two versions can be compared whatever order ties came back in.</summary>
    public static string Figures(PaymentDashboardDto d) => JsonSerializer.Serialize(new
    {
        d.TotalUnpaidCompleted, d.TotalUnpaidPending, d.FullyPaidPaymentCount, d.TotalFullyPaid, d.TotalWrittenOff,
        Completed = d.UnpaidCompletedReservations.OrderByDescending(r => r.ReservationDate).ThenBy(r => r.ReservationId),
        Pending = d.UnpaidPendingReservations.OrderByDescending(r => r.ReservationDate).ThenBy(r => r.ReservationId),
        BySource = d.AmountBySource.OrderBy(s => s.PatientSourceId ?? -1),
        ByClinic = d.AmountByClinic.OrderBy(c => c.ClinicId ?? -1),
        ByReason = d.WrittenOffByReason.OrderBy(r => r.Reason, StringComparer.Ordinal),
    });

    public static async Task SeedAsync(SafyaDbContext db, int reservations, int seed = 42, int batch = 2000)
    {
        var random = new Random(seed);
        var doctor = await db.Users.OrderBy(u => u.Id).Select(u => u.Id).FirstAsync();
        var statuses = await db.ReservationStatuses.ToDictionaryAsync(s => s.StatusName, s => s.Id);
        var treatment = await db.TreatmentTypes.OrderBy(t => t.Id).Select(t => t.Id).FirstAsync();

        var clinicA = await db.Clinics.OrderBy(c => c.Id).Select(c => c.Id).FirstAsync();
        var clinicB = new Clinic { Name = "Dashboard Clinic B" };
        var sourceA = new PatientSource { Name = "Dashboard Source A", DefaultDeductionPercentage = 10m };
        var sourceB = new PatientSource { Name = "Dashboard Source B", DefaultDeductionPercentage = 20m };
        db.AddRange(clinicB, sourceA, sourceB);
        var patients = Enumerable.Range(1, Math.Max(10, reservations / 10))
            .Select(i => new Patient { FirstName = $"Dash{i}", LastName = $"Patient{i}" }).ToList();
        db.Patients.AddRange(patients);
        await db.SaveChangesAsync();

        int?[] clinics = { clinicA, clinicB.Id, null };
        int?[] sources = { sourceA.Id, sourceB.Id, null };
        string[] statusNames = { "Pending", "Confirmed", "Completed", "Cancelled", "NoShow" };
        PatientQueueStatus[] queues =
        {
            PatientQueueStatus.NotCheckedIn, PatientQueueStatus.Waiting,
            PatientQueueStatus.InConsultation, PatientQueueStatus.Finished, PatientQueueStatus.Finished
        };
        decimal?[] prices = { 800m, 200m, 800m, 0m, null, 450m };
        WriteOffReason?[] reasons = { WriteOffReason.Discount, WriteOffReason.Hardship, null, WriteOffReason.Other };

        for (var done = 0; done < reservations; done += batch)
        {
            var visits = new List<Reservation>();
            for (var i = done; i < Math.Min(reservations, done + batch); i++)
            {
                var date = Start.AddDays(random.Next(0, 365)).Date;
                visits.Add(new Reservation
                {
                    PatientId = patients[random.Next(patients.Count)].Id,
                    DoctorId = doctor,
                    ClinicId = clinics[random.Next(2)]!.Value,
                    StatusId = statuses[statusNames[random.Next(statusNames.Length)]],
                    TreatmentTypeId = treatment,
                    ReservationDate = date,
                    ReservationTime = new TimeSpan(9 + random.Next(0, 10), 30 * random.Next(0, 2), 0),
                    DurationMinutes = 30,
                    TotalAmount = prices[random.Next(prices.Length)],
                    QueueStatus = queues[random.Next(queues.Length)],
                });
            }
            db.Reservations.AddRange(visits);
            await db.SaveChangesAsync();

            var payments = new List<Payment>();
            foreach (var visit in visits)
            {
                var price = visit.TotalAmount ?? 0m;
                var kind = random.Next(0, 7);   // 0 none, 1 partial, 2 full, 3 partial + write-off, 4 voided, 5 write-off only, 6 full in two parts
                var when = DateTime.SpecifyKind(visit.ReservationDate.AddHours(10 + random.Next(0, 8)), DateTimeKind.Utc);
                void Pay(decimal amount, PaymentStatusEnum status, WriteOffReason? reason = null)
                {
                    if (amount <= 0m) return;
                    var source = sources[random.Next(sources.Length)];
                    var deduction = source is null ? 0m : Math.Round(amount * 0.1m, 2);
                    payments.Add(new Payment
                    {
                        PatientId = visit.PatientId, ReservationId = visit.Id, CollectedBy = doctor,
                        ClinicId = clinics[random.Next(clinics.Length)], PatientSourceId = source,
                        Amount = amount, OriginalAmount = amount,
                        SourceDeductionAmount = status == PaymentStatusEnum.Active ? deduction : 0m,
                        ClinicNetAmount = status == PaymentStatusEnum.Active ? amount - deduction : 0m,
                        Status = status, WriteOffReason = reason,
                        PaymentMethod = status == PaymentStatusEnum.WrittenOff ? PaymentMethodEnum.WriteOff : PaymentMethodEnum.Cash,
                        PaymentDate = when,
                    });
                }
                switch (kind)
                {
                    case 1: Pay(Math.Round(price / 2, 2), PaymentStatusEnum.Active); break;
                    case 2: Pay(price, PaymentStatusEnum.Active); break;
                    case 3: Pay(Math.Round(price / 2, 2), PaymentStatusEnum.Active);
                            Pay(price - Math.Round(price / 2, 2), PaymentStatusEnum.WrittenOff, reasons[random.Next(reasons.Length)]); break;
                    case 4: Pay(price, PaymentStatusEnum.Cancelled); break;
                    case 5: Pay(Math.Round(price / 4, 2), PaymentStatusEnum.WrittenOff, reasons[random.Next(reasons.Length)]); break;
                    case 6: Pay(Math.Round(price / 3, 2), PaymentStatusEnum.Active); Pay(price - Math.Round(price / 3, 2), PaymentStatusEnum.Active); break;
                }
            }
            // Money with no visit (e.g. a nutrition package), now and then.
            for (var k = 0; k < visits.Count / 50; k++)
                payments.Add(new Payment
                {
                    PatientId = patients[random.Next(patients.Count)].Id, CollectedBy = doctor,
                    ClinicId = clinics[random.Next(clinics.Length)], PatientSourceId = sources[random.Next(sources.Length)],
                    Amount = 300m + k, OriginalAmount = 300m + k, ClinicNetAmount = 300m + k,
                    Status = PaymentStatusEnum.Active, PaymentMethod = PaymentMethodEnum.Cash,
                    PaymentDate = Start.AddDays(random.Next(0, 365)),
                });
            db.Payments.AddRange(payments);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
    }
}
