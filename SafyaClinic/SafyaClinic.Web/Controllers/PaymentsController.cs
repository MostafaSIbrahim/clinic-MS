using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Payment;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.Common;
using SafyaClinic.Web.Security;
using SafyaClinic.Application.Security;
using SafyaClinic.Web.Reports;
namespace SafyaClinic.Web.Controllers;

[Authorize] // Payment access: each action below declares its specific permission.
public class PaymentsController : BaseController
{
    private readonly IPaymentService _paymentService;
    private readonly IClinicService _clinicService;
    private readonly IReservationService _reservationService;

    public PaymentsController(IPaymentService paymentService,
        IClinicService clinicService, 
        IReservationService reservationService)
    {
        _paymentService = paymentService;
        _clinicService = clinicService;
        _reservationService = reservationService;
    }

    [RequirePermission(Permissions.Payments.View)]
    // M1c: the payment summary is the Payments tab of the patient workspace; collect, write off and the
    // payment adjustments come back here and land on the tab.
    public IActionResult PatientSummary(int patientId) =>
        RedirectToAction("Details", "Patients", new { id = patientId, tab = SafyaClinic.Web.Models.PatientTabs.Payments });

    [HttpGet]
    [RequirePermission(Permissions.Payments.View)]
    public async Task<IActionResult> Audit(
    int paymentId,
    [FromQuery] PaginationRequest pagination)
    {
        var result = await _paymentService.GetPaymentAuditAsync(
            paymentId, pagination);

        if (!result.IsSuccess || result.Data is null)
            return NotFound("Payment not found.");

        return PartialView("Audit", result.Data);
    }

    [HttpGet]
    [RequirePermission(Permissions.Payments.ViewReport)]
    public async Task<IActionResult> Report(DateTime? from, DateTime? to, bool run = false,
        [FromServices] IClinicClock? clock = null)
    {
        clock ??= ClinicClock.Cairo;
        var report = await RunReportAsync(from, to, run, clock);
        if (report.Forbidden) return Forbid();

        ViewBag.TodayOnly = report.TodayOnly;
        ViewBag.From = report.From;
        ViewBag.To = report.To;
        ViewBag.HasSearch = report.HasSearch;
        ViewBag.Clock = clock;
        // Failed report loads must not appear as a valid zero-total report.
        var active = report.Payments.Where(p => p.Status == "Active").ToList();
        ViewBag.Total = active.Sum(p => p.Amount);
        // M2d: totals by payment method and by doctor (collected money only).
        ViewBag.ByMethod = active.GroupBy(p => p.PaymentMethod)
            .Select(g => new ReportTotal(g.Key, g.Count(), g.Sum(p => p.Amount)))
            .OrderByDescending(t => t.Amount).ThenBy(t => t.Name, StringComparer.Ordinal).ToList();
        ViewBag.ByDoctor = active.GroupBy(p => p.DoctorName ?? "")
            .Select(g => new ReportTotal(g.Key, g.Count(), g.Sum(p => p.Amount)))
            .OrderByDescending(t => t.Amount).ThenBy(t => t.Name, StringComparer.Ordinal).ToList();
        return View(report.Payments);
    }

    /// <summary>M2d: the report's rows as a CSV file (UTF-8 with a byte-order mark, so Excel shows Arabic names).</summary>
    [HttpGet]
    [RequirePermission(Permissions.Payments.ViewReport)]
    public async Task<IActionResult> ReportCsv(DateTime? from, DateTime? to, [FromServices] IClinicClock? clock = null)
    {
        clock ??= ClinicClock.Cairo;
        var report = await RunReportAsync(from, to, run: true, clock);
        if (report.Forbidden) return Forbid();
        if (!report.HasSearch || !ModelState.IsValid)
            return RedirectToAction(nameof(Report), new { from, to, run = true });

        var csv = PaymentReportCsv.Write(report.Payments, clock);
        var name = $"payments-{report.From:yyyy-MM-dd}-to-{report.To:yyyy-MM-dd}.csv";
        return File(csv, "text/csv; charset=utf-8", name);
    }

    public sealed record ReportTotal(string Name, int Count, decimal Amount);

    private sealed record ReportRun(bool Forbidden, bool TodayOnly, bool HasSearch, DateTime? From, DateTime? To, IReadOnlyList<PaymentDto> Payments);

    // The report's scope comes from the signed-in user's permissions, never from the address. M2d: the clinic
    // clock (Clinic:TimeZone) decides "today" and the day boundaries, as everywhere else since F2.
    private async Task<ReportRun> RunReportAsync(DateTime? from, DateTime? to, bool run, IClinicClock clock)
    {
        var empty = Array.Empty<PaymentDto>();
        var todayOnly = !await CanAsync(Permissions.Payments.ReportAnyDate);
        var ownPaymentsOnly = !await CanAsync(Permissions.Payments.ReportAllDoctors);
        if (todayOnly)
        {
            // Payment access: ignore attempted historical ranges; retain explicit search-first loading.
            var requested = run || from.HasValue || to.HasValue;
            from = to = clock.Today;
            ModelState.Clear();
            if (!requested) return new ReportRun(false, true, false, from, to, empty);
        }
        if (ownPaymentsOnly && CurrentUserId <= 0) return new ReportRun(true, todayOnly, false, from, to, empty);
        if (to?.Date == DateTime.MaxValue.Date)
            ModelState.AddModelError(nameof(to), "Choose an earlier end date.");
        if (from.HasValue || to.HasValue)
        {
            // Require a complete, ordered range before running the financial report.
            if (!from.HasValue)
                ModelState.AddModelError(nameof(from), "Choose a start date.");
            if (!to.HasValue)
                ModelState.AddModelError(nameof(to), "Choose an end date.");
            if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
                ModelState.AddModelError(nameof(to), "The end date must be on or after the start date.");
        }
        var hasSearch = from.HasValue && to.HasValue;
        if (!ModelState.IsValid || !hasSearch)
            return new ReportRun(false, todayOnly, hasSearch, from, to, empty);

        // Inclusive clinic calendar dates become an exclusive UTC end boundary.
        var fromUtc = clock.DayStartUtc(from!.Value.Date);
        var toUtc = clock.DayStartUtc(to!.Value.Date.AddDays(1));
        var result = await _paymentService.GetPaymentsByDateRangeAsync(fromUtc, toUtc, ownPaymentsOnly ? CurrentUserId : null);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError("", "Could not load the payment report. Please try again.");
            return new ReportRun(false, todayOnly, hasSearch, from, to, empty);
        }
        return new ReportRun(false, todayOnly, hasSearch, from, to, result.Data!.ToList());
    }

    // M2d: the two one-off repair actions (recalculate paid status, back-fill zero-cost payments) are removed.
    // Since P6 every payment change recomputes coverage, so nothing needs them.

    // ── Dashboard ───────────────────────────────────────────────

    [HttpGet]
    [RequirePermission(Permissions.Payments.View)]
    public async Task<IActionResult> Dashboard(DateTime? from, DateTime? to, bool all = false,
        int completedPage = 1, int pendingPage = 1, [FromServices] IClinicClock? clock = null)
    {
        // Payment access: reception never loads paid totals or financial breakdowns.
        var fullDashboard = await CanAsync(Permissions.Payments.ViewFullDashboard);

        // M2b: the full dashboard's collected and written-off figures default to this month (clinic time);
        // "All time" (?all=true) or any chosen range is still possible. Unpaid lists always cover every balance owed.
        if (fullDashboard && !all && from is null && to is null)
        {
            var today = (clock ?? ClinicClock.Cairo).Today;
            from = new DateTime(today.Year, today.Month, 1);
            to = today;
        }
        if (!fullDashboard) { from = null; to = null; }

        var result = await _paymentService.GetPaymentDashboardAsync(from, to, unpaidOnly: !fullDashboard,
            pageSize: UnpaidPageSize, completedPage: completedPage, pendingPage: pendingPage);
        if (!result.IsSuccess) ModelState.AddModelError("", "Could not load unpaid balances. Please try again.");
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.All = all && from is null && to is null;
        return View(fullDashboard ? "Dashboard" : "Unpaid", result.IsSuccess ? result.Data : new PaymentDashboardDto());
    }

    /// <summary>M2b: rows per page in the unpaid lists.</summary>
    public const int UnpaidPageSize = 20;

    // ── Dashboard line drill-down (AJAX, rendered inside a modal) ─
    // Called when the user clicks a row in "Amount by Clinic" / "Amount by Patient
    // Source" on the dashboard. groupType is "clinic" or "source"; groupId is the
    // ClinicId/PatientSourceId of that row (omitted/null for the "No Clinic"/"No
    // Source" row). from/to are the date range the user enters in the popup prompt.
    [HttpGet]
    [RequirePermission(Permissions.Payments.ViewFullDashboard)]
    public async Task<IActionResult> DashboardLineDetails(string groupType, int? groupId, DateTime? from, DateTime? to)
    {
        var result = await _paymentService.GetDashboardLineDetailsAsync(groupType, groupId, from, to);
        if (!result.IsSuccess)
            return BadRequest(result.Errors.FirstOrDefault() ?? "Could not load payment details.");

        return PartialView("_DashboardLineDetails", result.Data);
    }

    // ── Collect ─────────────────────────────────────────────────

    [HttpGet]
    [RequirePermission(Permissions.Payments.Collect)]
    public async Task<IActionResult> Collect(
    int patientId,
    int? reservationId,
    int? enrollmentId)
    {
        var clinicId = 0;

        if (reservationId.HasValue)
        {
            var reservationResult =
                await _reservationService.GetReservationByIdAsync(reservationId.Value);

            if (!reservationResult.IsSuccess ||
                reservationResult.Data is null ||
                reservationResult.Data.PatientId != patientId)
            {
                Error("Reservation not found for this patient.");
                return RedirectToAction(nameof(PatientSummary), new { patientId });
            }

            clinicId = reservationResult.Data.ClinicId;
        }

        var dueResult = await _paymentService.GetDueAmountAsync(
            patientId, reservationId, enrollmentId);

        if (!dueResult.IsSuccess)
        {
            Error(dueResult.Errors.FirstOrDefault() ?? "Could not load the amount due.");
            return RedirectToAction(nameof(PatientSummary), new { patientId });
        }
        if (dueResult.Data <= 0m)
        {
            Error(
                "The selected reservation or nutrition enrollment has no collectible balance.");

            if (reservationId.HasValue)
            {
                return RedirectToAction(
                    "Details",
                    "Reservations",
                    new { id = reservationId.Value });
            }

            return RedirectToAction(
                nameof(PatientSummary),
                new { patientId });
        }
        ViewBag.Clinics = (await _clinicService.GetAllAsync(
            includeInactive: reservationId.HasValue)).Data;

        return View(new CollectPaymentRequest
        {
            PatientId = patientId,
            ReservationId = reservationId,
            EnrollmentId = enrollmentId,
            ClinicId = clinicId,
            Amount = dueResult.Data
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.Payments.Collect)]
    public async Task<IActionResult> Collect(CollectPaymentRequest model)
    {
        if (CurrentUserId <= 0)
        {
            Error("Session expired. Please login again.");
            return RedirectToAction("Login", "Auth");
        }
        if (!ModelState.IsValid)
        {
            ViewBag.Clinics = (await _clinicService.GetAllAsync(
                   includeInactive: model.ReservationId.HasValue)).Data;
            return View(model);
        }

        var result = await _paymentService.CollectPaymentAsync(model, CurrentUserId);
        if (!result.IsSuccess)
        {
            ApplyErrors(result);
            ViewBag.Clinics = (await _clinicService.GetAllAsync(
                   includeInactive: model.ReservationId.HasValue)).Data;
            return View(model);
        }

        var message = result.Data!.IsFirstVisitDeduction
            ? $"Payment of {Money.Format(result.Data.Amount)} collected. First-visit source deduction of {Money.Format(result.Data.SourceDeductionAmount)} ({result.Data.DeductionPercentage}%) applied."
            : $"Payment of {Money.Format(result.Data.Amount)} collected.";

        return RedirectWithSuccess(
            message,
            nameof(PatientSummary),
            routeValues: new { patientId = model.PatientId });
    }

    // ── Cancel ──────────────────────────────────────────────────

    [HttpGet]
    [RequirePermission(Permissions.Payments.Adjust)]
    public async Task<IActionResult> Cancel(int paymentId)
    {
        var result = await _paymentService.GetPaymentByIdAsync(paymentId);
        if (!result.IsSuccess) { Error("Payment not found."); return RedirectToAction(nameof(Report)); }
        ViewBag.Payment = result.Data;   // M2c: the patient header and the way back to their Payments tab
        return View(new CancelPaymentRequest { PaymentId = paymentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.Payments.Adjust)]
    public async Task<IActionResult> Cancel(CancelPaymentRequest model)
    {
        if (!ModelState.IsValid) return await PaymentFormAgainAsync(model.PaymentId, model);
        var result = await _paymentService.CancelPaymentAsync(model, CurrentUserId);
        if (!result.IsSuccess) { ApplyErrors(result); return await PaymentFormAgainAsync(model.PaymentId, model); }
        return RedirectWithSuccess(
            "Payment cancelled.",
            nameof(PatientSummary),
            routeValues: new { patientId = result.Data!.PatientId });
    }

    // ── Write off (P6b) ─────────────────────────────────────────

    [HttpGet]
    [RequirePermission(Permissions.Payments.WriteOff)]
    public async Task<IActionResult> WriteOff(int patientId, int reservationId)
    {
        var due = await _paymentService.GetDueAmountAsync(patientId, reservationId, null);
        if (!due.IsSuccess || due.Data <= 0m)
        {
            Error(due.IsSuccess ? "This visit has no balance to write off." : due.Errors.FirstOrDefault() ?? "Could not load the balance.");
            return RedirectToAction(nameof(PatientSummary), new { patientId });
        }
        ViewBag.Due = due.Data;
        return View(new WriteOffRequest { PatientId = patientId, ReservationId = reservationId, Amount = due.Data });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.Payments.WriteOff)]
    public async Task<IActionResult> WriteOff(WriteOffRequest model)
    {
        var result = await _paymentService.WriteOffAsync(model, CurrentUserId);
        if (!result.IsSuccess)
        {
            ApplyErrors(result);
            var due = await _paymentService.GetDueAmountAsync(model.PatientId, model.ReservationId, null);
            ViewBag.Due = due.IsSuccess ? due.Data : 0m;
            return View(model);
        }
        return RedirectWithSuccess(
            $"{Money.Format(result.Data!.Amount)} written off for reservation #{model.ReservationId}.",
            nameof(PatientSummary),
            routeValues: new { patientId = model.PatientId });
    }

    [HttpGet]
    [RequirePermission(Permissions.Payments.WriteOff)]
    public async Task<IActionResult> CancelWriteOff(int paymentId)
    {
        var result = await _paymentService.GetPaymentByIdAsync(paymentId);
        if (!result.IsSuccess || result.Data is null || result.Data.Status != "WrittenOff")
        {
            Error("Write-off not found.");
            return RedirectToAction(nameof(Report));
        }
        ViewBag.Payment = result.Data;
        return View(new CancelWriteOffRequest { PaymentId = paymentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.Payments.WriteOff)]
    public async Task<IActionResult> CancelWriteOff(CancelWriteOffRequest model)
    {
        var result = await _paymentService.CancelWriteOffAsync(model, CurrentUserId);
        if (!result.IsSuccess)
        {
            ApplyErrors(result);
            ViewBag.Payment = (await _paymentService.GetPaymentByIdAsync(model.PaymentId)).Data;
            return View(model);
        }
        return RedirectWithSuccess(
            "Write-off undone. The amount is due again.",
            nameof(PatientSummary),
            routeValues: new { patientId = result.Data!.PatientId });
    }

    // ── Change amount ───────────────────────────────────────────

    [HttpGet]
    [RequirePermission(Permissions.Payments.Adjust)]
    public async Task<IActionResult> ChangeAmount(int paymentId)
    {
        var result = await _paymentService.GetPaymentByIdAsync(paymentId);
        if (!result.IsSuccess) { Error("Payment not found."); return RedirectToAction(nameof(Report)); }
        ViewBag.Payment = result.Data;   // M2c
        return View(new ChangePaymentAmountRequest { PaymentId = paymentId, NewAmount = result.Data!.Amount });
    }

    // M2c: a refused cancel or amount change shows the form again with the payment it's about (before, the
    // amount-change form failed to show again, because the current amount was missing).
    private async Task<IActionResult> PaymentFormAgainAsync(int paymentId, object model)
    {
        var payment = await _paymentService.GetPaymentByIdAsync(paymentId);
        ViewBag.Payment = payment.IsSuccess ? payment.Data : null;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.Payments.Adjust)]
    public async Task<IActionResult> ChangeAmount(ChangePaymentAmountRequest model)
    {
        if (!ModelState.IsValid) return await PaymentFormAgainAsync(model.PaymentId, model);
        var result = await _paymentService.ChangePaymentAmountAsync(model, CurrentUserId);
        if (!result.IsSuccess) { ApplyErrors(result); return await PaymentFormAgainAsync(model.PaymentId, model); }
        return RedirectWithSuccess(
            "Payment amount updated.",
            nameof(PatientSummary),
            routeValues: new { patientId = result.Data!.PatientId });
    }
}
