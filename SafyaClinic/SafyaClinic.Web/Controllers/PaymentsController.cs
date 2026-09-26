using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Payment;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.DTOs.Common;

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

    [Authorize(Policy = "PaymentStaff")]
    public async Task<IActionResult> PatientSummary(int patientId)
    {
        var result = await _paymentService.GetPatientFinancialSummaryAsync(patientId);
        if (!result.IsSuccess) return RedirectToAction("Details", "Patients", new { id = patientId });
        return View(result.Data);
    }

    [HttpGet]
    [Authorize(Policy = "PaymentStaff")]
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
    [Authorize(Policy = "PaymentReports")]
    public async Task<IActionResult> Report(DateTime? from, DateTime? to, bool run = false)
    {
        // Payment access: derive scope from authenticated roles, never query-string doctor IDs.
        var todayOnly = !IsAdmin && !IsDoctor;
        var reportZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        ViewBag.TodayOnly = todayOnly;
        ViewBag.ReportTimeZone = reportZone;
        if (todayOnly)
        {
            // Payment access: ignore attempted historical ranges; retain explicit search-first loading.
            var requested = run || from.HasValue || to.HasValue;
            from = to = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, reportZone).Date;
            ViewBag.From = from;
            ViewBag.To = to;
            ViewBag.HasSearch = requested;
            ModelState.Clear();
            if (!requested) return View(Enumerable.Empty<PaymentDto>());
        }
        if (!IsAdmin && IsDoctor && CurrentUserId <= 0) return Forbid();
        if (to?.Date == DateTime.MaxValue.Date)
            ModelState.AddModelError(nameof(to), "Choose an earlier end date.");
        // Search-first: do not substitute a default range and query payments on initial navigation.
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.HasSearch = from.HasValue && to.HasValue;
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
        if (!ModelState.IsValid || !from.HasValue || !to.HasValue)
            return View(Enumerable.Empty<PaymentDto>());

        // Payment access: inclusive Cairo calendar dates become an exclusive UTC end boundary.
        var fromUtc = ReportDayStartUtc(from.Value.Date, reportZone);
        var toUtc = ReportDayStartUtc(to.Value.Date.AddDays(1), reportZone);
        var result = await _paymentService.GetPaymentsByDateRangeAsync(
            fromUtc, toUtc, !IsAdmin && IsDoctor ? CurrentUserId : null);
        // Failed report loads must not appear as a valid zero-total report.
        if (!result.IsSuccess)
            ModelState.AddModelError("", "Could not load the payment report. Please try again.");
        ViewBag.Total = result.IsSuccess ? result.Data!.Where(p => p.Status == "Active").Sum(p => p.Amount) : 0m;
        return View(result.IsSuccess ? result.Data : Enumerable.Empty<PaymentDto>());
    }

    // Payment access: Cairo's spring transition skips midnight; use the first real instant of that date.
    private static DateTime ReportDayStartUtc(DateTime date, TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        if (zone.IsAmbiguousTime(local))
            return new DateTimeOffset(local, zone.GetAmbiguousTimeOffsets(local).Max()).UtcDateTime;
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    // ── One-time maintenance: backfill historical IsPaid values ──
    // Corrects Reservation.IsPaid for rows written before the save-ordering fix in
    // PaymentService (see CollectPaymentAsync / ChangePaymentAmountAsync). Safe to run
    // more than once — it only updates rows whose computed status actually differs.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RecalculateAllPaidStatuses()
    {
        var result = await _paymentService.RecalculateAllReservationsPaidStatusAsync();
        if (!result.IsSuccess)
        {
            ApplyErrors(result);
            Error("Failed to recalculate reservation paid statuses.");
        }
        else
        {
            Success(result.Message);
        }
        return RedirectToAction(nameof(Report));
    }

    // ── One-time maintenance: backfill $0 payments for pre-existing free reservations ──
    // Reservations for a zero-cost treatment type (e.g. nutrition "Follow-up") created
    // before ReservationService started auto-recording a $0 payment were left with no
    // Payment row at all, so they never appeared in payment reports/dashboards. Safe to
    // run more than once — it only creates a payment for reservations that don't already
    // have one.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> BackfillZeroCostPayments()
    {
        var result = await _paymentService.BackfillZeroCostPaymentsAsync(CurrentUserId);
        if (!result.IsSuccess)
        {
            ApplyErrors(result);
            Error("Failed to backfill zero-cost payments.");
        }
        else
        {
            Success(result.Message);
        }
        return RedirectToAction(nameof(Report));
    }

    // ── Dashboard ───────────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = "PaymentStaff")]
    public async Task<IActionResult> Dashboard(DateTime? from, DateTime? to)
    {
        // Payment access: reception never loads paid totals or financial breakdowns.
        var result = await _paymentService.GetPaymentDashboardAsync(IsAdmin ? from : null, IsAdmin ? to : null, unpaidOnly: !IsAdmin);
        if (!result.IsSuccess) ModelState.AddModelError("", "Could not load unpaid balances. Please try again.");
        ViewBag.From = from;
        ViewBag.To = to;
        return View(IsAdmin ? "Dashboard" : "Unpaid", result.IsSuccess ? result.Data : new PaymentDashboardDto());
    }

    // ── Dashboard line drill-down (AJAX, rendered inside a modal) ─
    // Called when the user clicks a row in "Amount by Clinic" / "Amount by Patient
    // Source" on the dashboard. groupType is "clinic" or "source"; groupId is the
    // ClinicId/PatientSourceId of that row (omitted/null for the "No Clinic"/"No
    // Source" row). from/to are the date range the user enters in the popup prompt.
    [HttpGet]
    [Authorize(Policy = "AdminOnly")] // Payment access: protect direct drill-down requests too.
    public async Task<IActionResult> DashboardLineDetails(string groupType, int? groupId, DateTime? from, DateTime? to)
    {
        var result = await _paymentService.GetDashboardLineDetailsAsync(groupType, groupId, from, to);
        if (!result.IsSuccess)
            return BadRequest(result.Errors.FirstOrDefault() ?? "Could not load payment details.");

        return PartialView("_DashboardLineDetails", result.Data);
    }

    // ── Collect ─────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = "PaymentStaff")]
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
    [Authorize(Policy = "PaymentStaff")]
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
            ? $"Payment of {result.Data.Amount:C} collected. First-visit source deduction of {result.Data.SourceDeductionAmount:C} ({result.Data.DeductionPercentage}%) applied."
            : $"Payment of {result.Data.Amount:C} collected.";

        return RedirectWithSuccess(
            message,
            nameof(PatientSummary),
            routeValues: new { patientId = model.PatientId });
    }

    // ── Cancel ──────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = "PaymentStaff")]
    public async Task<IActionResult> Cancel(int paymentId)
    {
        var result = await _paymentService.GetPaymentByIdAsync(paymentId);
        if (!result.IsSuccess) { Error("Payment not found."); return RedirectToAction(nameof(Report)); }
        return View(new CancelPaymentRequest { PaymentId = paymentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "PaymentStaff")]
    public async Task<IActionResult> Cancel(CancelPaymentRequest model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _paymentService.CancelPaymentAsync(model, CurrentUserId);
        if (!result.IsSuccess) { ApplyErrors(result); return View(model); }
        return RedirectWithSuccess(
            "Payment cancelled.",
            nameof(PatientSummary),
            routeValues: new { patientId = result.Data!.PatientId });
    }

    // ── Change amount ───────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = "PaymentStaff")]
    public async Task<IActionResult> ChangeAmount(int paymentId)
    {
        var result = await _paymentService.GetPaymentByIdAsync(paymentId);
        if (!result.IsSuccess) { Error("Payment not found."); return RedirectToAction(nameof(Report)); }
        ViewBag.CurrentAmount = result.Data!.Amount;
        return View(new ChangePaymentAmountRequest { PaymentId = paymentId, NewAmount = result.Data.Amount });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "PaymentStaff")]
    public async Task<IActionResult> ChangeAmount(ChangePaymentAmountRequest model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _paymentService.ChangePaymentAmountAsync(model, CurrentUserId);
        if (!result.IsSuccess) { ApplyErrors(result); return View(model); }
        return RedirectWithSuccess(
            "Payment amount updated.",
            nameof(PatientSummary),
            routeValues: new { patientId = result.Data!.PatientId });
    }
}
