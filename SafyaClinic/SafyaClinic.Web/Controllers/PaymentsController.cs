using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Payment;
using SafyaClinic.Application.Interfaces.Services;
using System.Security.Claims;

namespace SafyaClinic.Web.Controllers;

[Authorize(Policy = "ReceptionOrAdmin")]
public class PaymentsController : BaseController
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService) =>
        _paymentService = paymentService;

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
        ViewBag.Total = result.IsSuccess ? result.Data!.Sum(p => p.Amount) : 0m;
        return View(result.IsSuccess ? result.Data : Enumerable.Empty<PaymentDto>());
    }

    [HttpGet]
    public IActionResult Collect(int patientId, int? reservationId, int? enrollmentId)
    {
        return View(new CollectPaymentRequest
        {
            PatientId = patientId,
            ReservationId = reservationId,
            EnrollmentId = enrollmentId,
           // CollectedBy = CurrentUserId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Collect(CollectPaymentRequest model)
    {

        /*var nameId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var currentId = CurrentUserId;
        ModelState.AddModelError("", $"DEBUG — NameIdentifier: '{nameId}', CurrentUserId: {currentId}");*/

        /*System.Diagnostics.Debug.WriteLine($"NameIdentifier raw: {nameId}");
        System.Diagnostics.Debug.WriteLine($"CurrentUserId parsed: {CurrentUserId}");
        System.Diagnostics.Debug.WriteLine($"All claims: {string.Join(", ", allClaims)}");*/
        if (CurrentUserId<= 0)
        {
            Error("Session expired. Please login again.");
            return RedirectToAction("Login", "Auth");
        }
        if (!ModelState.IsValid) return View(model);

        var result = await _paymentService.CollectPaymentAsync(model, CurrentUserId);
        if (!result.IsSuccess) { ApplyErrors(result); return View(model); }

        return RedirectWithSuccess(
            $"Payment of {result.Data!.Amount:C} collected.",
            nameof(PatientSummary),
            routeValues: new { patientId = model.PatientId });
    }
}