using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SafyaClinic.Web.Models;
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
    private readonly IPatientService _patientService;

    public ReservationsController(
        IReservationService reservationService,
        IUserService userService,
        IClinicService clinicService,
        IPatientService patientService)
    {
        _reservationService = reservationService;
        _userService = userService;
        _clinicService = clinicService;
        _patientService = patientService;
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

    [HttpGet]
    [Authorize(Roles = "Admin,Reception,Doctor")]
    public async Task<IActionResult> Queue(int? clinicId, int? doctorId)
    {
        if (!ModelState.IsValid ||
            clinicId is <= 0 ||
            doctorId is <= 0)
        {
            return BadRequest("Invalid queue filters.");
        }

        if (CurrentUserId <= 0)
            return Challenge();

        var canViewAllQueues = IsAdmin || IsReception;

        // Doctors cannot override their own queue through URL parameters.
        if (!canViewAllQueues)
            doctorId = CurrentUserId;

        var clinicsResult = await _clinicService.GetAllAsync(
            includeInactive: true);

        if (!clinicsResult.IsSuccess || clinicsResult.Data is null)
        {
            Error("Could not load clinic filters.");
            return RedirectToAction(nameof(Index));
        }

        var clinics = clinicsResult.Data.ToList();

        if (clinicId.HasValue &&
            !clinics.Any(c => c.Id == clinicId.Value))
        {
            return BadRequest("Invalid clinic.");
        }

        var doctorOptions = new List<QueueDoctorOptionDto>();

        if (canViewAllQueues)
        {
            doctorOptions = await _reservationService
                .GetQueueDoctorOptionsAsync(clinicId);

            // Reset a doctor selection that no longer belongs to the filters.
            if (doctorId.HasValue &&
                !doctorOptions.Any(d => d.DoctorId == doctorId.Value))
            {
                doctorId = null;
                ModelState.Remove(nameof(doctorId));
            }
        }

        var queueResult = await _reservationService.GetDoctorQueueAsync(
            clinicId,
            doctorId);

        if (!queueResult.IsSuccess || queueResult.Data is null)
        {
            Error(queueResult.Errors.FirstOrDefault()
                ?? "Could not load the queue.");

            return RedirectToAction(nameof(Index));
        }

        return View(new DoctorQueueViewModel
        {
            ClinicId = clinicId,
            DoctorId = doctorId,
            CanViewAllQueues = canViewAllQueues,
            Entries = queueResult.Data,

            Clinics = clinics
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                })
                .ToList(),

            Doctors = doctorOptions
                .Select(d => new SelectListItem
                {
                    Value = d.DoctorId.ToString(),
                    Text = d.DoctorName
                })
                .ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Board(
    DateTime? date,
    int? doctorId,
    int? clinicId)
    {
        if (!ModelState.IsValid)
            return BadRequest("Invalid board filters.");

        var result = await _reservationService.GetAppointmentBoardAsync(
            date ?? DateTime.Today, doctorId, clinicId);

        if (!result.IsSuccess || result.Data is null)
        {
            Error(result.Errors.FirstOrDefault() ?? "Could not load appointment board.");
            return RedirectToAction(nameof(Index));
        }

        var doctors = await _userService.GetDoctorsAsync();
        var clinics = await _clinicService.GetAllAsync(includeInactive: true);

        if (!doctors.IsSuccess || !clinics.IsSuccess)
        {
            Error("Could not load board filters.");
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Doctors = doctors.Data;
        ViewBag.Clinics = clinics.Data;

        return View(result.Data);
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
        if (patientId.HasValue)
        {
            var patientResult = await _patientService.GetPatientByIdAsync(patientId.Value);

            if (!patientResult.IsSuccess || patientResult.Data is null)
            {
                Error("Patient not found.");
                return RedirectToAction("Index", "Patients");
            }

            ViewBag.SelectedPatientName = patientResult.Data.FullName;
        }
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
            if (model.PatientId > 0)
            {
                var patientResult = await _patientService.GetPatientByIdAsync(model.PatientId);
                ViewBag.SelectedPatientName = patientResult.Data?.FullName;
            }
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
            if (model.PatientId > 0)
            {
                var patientResult = await _patientService.GetPatientByIdAsync(model.PatientId);
                ViewBag.SelectedPatientName = patientResult.Data?.FullName;
            }
            return View(model);
        }
        return RedirectWithSuccess("Reservation booked.", nameof(Details), routeValues: new { id = result.Data!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _reservationService.GetReservationByIdAsync(id);
        if (!result.IsSuccess) return RedirectToAction(nameof(Index));
        if (result.Data!.StatusName == "Completed")
        {
            Error("Completed reservations cannot be edited.");
            return RedirectToAction(nameof(Details), new { id });
        }
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
        if (!result.IsSuccess)
        {
            ApplyErrors(result);
            ViewBag.Doctors = (await _userService.GetDoctorsAsync()).Data;
            ViewBag.Clinics =
                (await _clinicService.GetAllAsync(includeInactive: false)).Data;
            ViewBag.TreatmentTypes =
                (await _reservationService.GetTreatmentTypesAsync()).Data;

            return View(model);
        }
        return RedirectWithSuccess("Reservation updated.", nameof(Details), routeValues: new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, int statusId)
    {
        var result = await _reservationService.UpdateStatusAsync(id, statusId);

        if (!result.IsSuccess)
        {
            Error(result.Errors.FirstOrDefault() ?? "Could not update reservation status.");
            return RedirectToAction(nameof(Details), new { id });
        }

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
        var result = await _reservationService
            .CancelReservationAsync(id, reason);

        if (!result.IsSuccess)
        {
            Error(result.Errors.FirstOrDefault()
                ?? "Could not cancel the reservation.");

            return RedirectToAction(nameof(Index));
        }

        return RedirectWithSuccess(
            "Reservation cancelled.",
            nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> PatientReservationHistoryReport(int patientId)
    {
        if (patientId <= 0)
        {
            return BadRequest("معرف المريض غير صالح.");
        }

        var patientResult = await _patientService.GetPatientByIdAsync(patientId);
        if (patientResult == null || !patientResult.IsSuccess || patientResult.Data == null)
        {
            return NotFound("لم يتم العثور على بيانات المريض.");
        }

        var reservationsResult =
     await _reservationService.GetPatientReservationHistoryAsync(patientId);

        if (!reservationsResult.IsSuccess || reservationsResult.Data is null)
        {
            Error("Could not load reservation history.");
            return RedirectToAction(
                "Details", "Patients", new { id = patientId });
        }

        ViewBag.Patient = patientResult.Data;
        return View(reservationsResult.Data);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "ReceptionOrAdmin")]
    public async Task<IActionResult> CheckIn(int id)
    {
        var result = await _reservationService.CheckInAsync(id);

        if (result.IsSuccess)
        {
            Success(result.Message);
        }
        else
        {
            Error(result.Errors.FirstOrDefault() ?? "Check-in failed.");
        }

        return RedirectToAction(nameof(Details), new { id });
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "DoctorOrAdmin")]
    public async Task<IActionResult> StartConsultation(int id)
    {
        var result = await _reservationService.StartConsultationAsync(
            id, CurrentUserId, IsAdmin);

        if (result.IsSuccess)
            Success(result.Message);
        else
            Error(result.Errors.FirstOrDefault() ?? "Could not start consultation.");

        return RedirectToAction(nameof(Details), new { id });
    }
}