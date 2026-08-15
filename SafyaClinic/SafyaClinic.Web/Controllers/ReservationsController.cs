using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Reservation;
using SafyaClinic.Application.Interfaces.Services;

namespace SafyaClinic.Web.Controllers;

[Authorize(Policy = "ClinicalStaff")]
public class ReservationsController : BaseController
{
    private readonly IReservationService _reservationService;
    private readonly IUserService _userService;
    private readonly IClinicService _clinicService;

    public ReservationsController(
        IReservationService reservationService,
        IUserService userService,
        IClinicService clinicService)
    {
        _reservationService = reservationService;
        _userService = userService;
        _clinicService = clinicService;
    }

    public async Task<IActionResult> Index(
        [FromQuery] ReservationFilterRequest filter,
        [FromQuery] PaginationRequest pagination)
    {
        var result = await _reservationService.GetReservationsAsync(filter, pagination);
        ViewBag.Filter = filter;
        ViewBag.Pagination = pagination;
        return View(result.IsSuccess ? result.Data : null);
    }

    public async Task<IActionResult> Today([FromQuery] int? doctorId)
    {
        var result = await _reservationService.GetTodayReservationsAsync(doctorId);
        return View(result.IsSuccess ? result.Data : Enumerable.Empty<ReservationSummaryDto>());
    }

    public async Task<IActionResult> Details(int id)
    {
        var result = await _reservationService.GetReservationByIdAsync(id);
        if (!result.IsSuccess) { Error("Reservation not found."); return RedirectToAction(nameof(Index)); }
        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? patientId)
    {
        var doctors = await _userService.GetDoctorsAsync();
        ViewBag.Doctors = doctors.Data;
        var clinics = await _clinicService.GetAllAsync(includeInactive: false);
        ViewBag.Clinics = clinics.Data;
        var treatmentTypes = await _reservationService.GetTreatmentTypesAsync();
        ViewBag.TreatmentTypes = treatmentTypes.Data;
        ViewBag.PatientId = patientId;
        return View(new CreateReservationRequest
        {
            PatientId = patientId ?? 0,
            ReservationDate = DateTime.Today,
            ReservationTime = new TimeSpan(9, 0, 0)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateReservationRequest model)
    {
        if (!ModelState.IsValid)
        {
            var d = await _userService.GetDoctorsAsync();
            ViewBag.Doctors = d.Data;
            ViewBag.Clinics = (await _clinicService.GetAllAsync(includeInactive: false)).Data;
            ViewBag.TreatmentTypes = (await _reservationService.GetTreatmentTypesAsync()).Data;
            return View(model);
        }
        var result = await _reservationService.CreateReservationAsync(model, CurrentUserId);
        if (!result.IsSuccess)
        {
            ApplyErrors(result);
            var d = await _userService.GetDoctorsAsync();
            ViewBag.Doctors = d.Data;
            ViewBag.Clinics = (await _clinicService.GetAllAsync(includeInactive: false)).Data;
            ViewBag.TreatmentTypes = (await _reservationService.GetTreatmentTypesAsync()).Data;
            return View(model);
        }
        return RedirectWithSuccess("Reservation booked.", nameof(Details), routeValues: new { id = result.Data!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _reservationService.GetReservationByIdAsync(id);
        if (!result.IsSuccess) return RedirectToAction(nameof(Index));
        var doctors = await _userService.GetDoctorsAsync();
        ViewBag.Doctors = doctors.Data;
        ViewBag.Clinics = (await _clinicService.GetAllAsync(includeInactive: false)).Data;
        ViewBag.TreatmentTypes = (await _reservationService.GetTreatmentTypesAsync()).Data;
        var r = result.Data!;
        return View(new UpdateReservationRequest
        {
            DoctorId = r.DoctorId,
            ClinicId = r.ClinicId,
            TreatmentTypeId = r.TreatmentTypeId,
            StatusId = 1,
            ReservationDate = r.ReservationDate,
            ReservationTime = r.ReservationTime,
            DurationMinutes = r.DurationMinutes,
            Reason = r.Reason,
            Notes = r.Notes,
            TotalAmount = r.TotalAmount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateReservationRequest model)
    {
        if (!ModelState.IsValid)
        {
            var d = await _userService.GetDoctorsAsync();
            ViewBag.Doctors = d.Data;
            ViewBag.Clinics = (await _clinicService.GetAllAsync(includeInactive: false)).Data;
            ViewBag.TreatmentTypes = (await _reservationService.GetTreatmentTypesAsync()).Data;
            return View(model);
        }
        var result = await _reservationService.UpdateReservationAsync(id, model);
        if (!result.IsSuccess) { ApplyErrors(result); return View(model); }
        return RedirectWithSuccess("Reservation updated.", nameof(Details), routeValues: new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, int statusId)
    {
        var result = await _reservationService.UpdateStatusAsync(id, statusId);

        if (!result.IsSuccess)
        {
            ApplyErrors(result);
            return RedirectToAction(nameof(Details), new { id });
        }

        // StatusId 3 = Completed (see ReservationStatus seed data). Prompt reception
        // to collect the treatment's price right away instead of leaving it to be
        // found later on the payment dashboard.
        if (statusId == 3 && result.Data is { IsPaid: false } r)
        {
            return RedirectToAction("Collect", "Payments", new { patientId = r.PatientId, reservationId = id });
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPaid(int id)
    {
        await _reservationService.MarkAsPaidAsync(id);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        await _reservationService.CancelReservationAsync(id, reason);
        return RedirectWithSuccess("Reservation cancelled.", nameof(Index));
    }
}