using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Payment;
using SafyaClinic.Application.Interfaces.Services;

namespace SafyaClinic.Web.Controllers;

[Authorize(Policy = "ReceptionOrAdmin")]
public class PaymentsController : BaseController
{
    private readonly IPaymentService _paymentService;
    private readonly IClinicService _clinicService;

    public PaymentsController(IPaymentService paymentService, IClinicService clinicService)
    {
        _paymentService = paymentService;
        _clinicService = clinicService;
    }

    public async Task<IActionResult> PatientSummary(int patientId)
    {
        var result = await _paymentService.GetPatientFinancialSummaryAsync(patientId);
        if (!result.IsSuccess) return RedirectToAction("Details", "Patients", new { id = patientId });
        return View(result.Data);
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Report(DateTime? from, DateTime? to)
    {
        var f = from ?? DateTime.Today.AddMonths(-1);
        var t = to ?? DateTime.Today;
        var result = await _paymentService.GetPaymentsByDateRangeAsync(f, t);
        ViewBag.From = f;
        ViewBag.To = t;
        ViewBag.Total = result.IsSuccess ? result.Data!.Where(p => p.Status == "Active").Sum(p => p.Amount) : 0m;
        return View(result.IsSuccess ? result.Data : Enumerable.Empty<PaymentDto>());
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

    // ── Dashboard ───────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Dashboard(DateTime? from, DateTime? to)
    {
        var result = await _paymentService.GetPaymentDashboardAsync(from, to);
        ViewBag.From = from;
        ViewBag.To = to;
        return View(result.IsSuccess ? result.Data : new PaymentDashboardDto());
    }

    // ── Dashboard line drill-down (AJAX, rendered inside a modal) ─
    // Called when the user clicks a row in "Amount by Clinic" / "Amount by Patient
    // Source" on the dashboard. groupType is "clinic" or "source"; groupId is the
    // ClinicId/PatientSourceId of that row (omitted/null for the "No Clinic"/"No
    // Source" row). from/to are the date range the user enters in the popup prompt.
    [HttpGet]
    public async Task<IActionResult> DashboardLineDetails(string groupType, int? groupId, DateTime? from, DateTime? to)
    {
        var result = await _paymentService.GetDashboardLineDetailsAsync(groupType, groupId, from, to);
        if (!result.IsSuccess)
            return BadRequest(result.Errors.FirstOrDefault() ?? "Could not load payment details.");

        return PartialView("_DashboardLineDetails", result.Data);
    }

    // ── Collect ─────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Collect(int patientId, int? reservationId, int? enrollmentId)
    {
        var clinics = await _clinicService.GetAllAsync(includeInactive: false);
        ViewBag.Clinics = clinics.Data;

        var dueResult = await _paymentService.GetDueAmountAsync(patientId, reservationId, enrollmentId);

        return View(new CollectPaymentRequest
        {
            PatientId = patientId,
            ReservationId = reservationId,
            EnrollmentId = enrollmentId,
            Amount = dueResult.IsSuccess ? dueResult.Data : 0m
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Collect(CollectPaymentRequest model)
    {
        if (CurrentUserId <= 0)
        {
            Error("Session expired. Please login again.");
            return RedirectToAction("Login", "Auth");
        }
        if (!ModelState.IsValid)
        {
            ViewBag.Clinics = (await _clinicService.GetAllAsync(includeInactive: false)).Data;
            return View(model);
        }

        var result = await _paymentService.CollectPaymentAsync(model, CurrentUserId);
        if (!result.IsSuccess)
        {
            ApplyErrors(result);
            ViewBag.Clinics = (await _clinicService.GetAllAsync(includeInactive: false)).Data;
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
    public async Task<IActionResult> Cancel(int paymentId)
    {
        var result = await _paymentService.GetPaymentByIdAsync(paymentId);
        if (!result.IsSuccess) { Error("Payment not found."); return RedirectToAction(nameof(Report)); }
        return View(new CancelPaymentRequest { PaymentId = paymentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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
    public async Task<IActionResult> ChangeAmount(int paymentId)
    {
        var result = await _paymentService.GetPaymentByIdAsync(paymentId);
        if (!result.IsSuccess) { Error("Payment not found."); return RedirectToAction(nameof(Report)); }
        ViewBag.CurrentAmount = result.Data!.Amount;
        return View(new ChangePaymentAmountRequest { PaymentId = paymentId, NewAmount = result.Data.Amount });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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
