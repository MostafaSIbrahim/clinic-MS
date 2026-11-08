using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SafyaClinic.Web.Models;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Reservation;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Web.Security;
using SafyaClinic.Application.Security;

namespace SafyaClinic.Web.Controllers;

[RequirePermission(Permissions.Reservations.View)]
public class ReservationsController : BaseController
{
    private readonly IReservationService _reservationService;
    private readonly IUserService _userService;
    private readonly IClinicService _clinicService;
    private readonly IPatientService _patientService;
    private readonly IPaymentService _paymentService;


    public ReservationsController(
        IReservationService reservationService,
        IUserService userService,
        IClinicService clinicService,
        IPatientService patientService,
        IPaymentService paymentService)
    {
        _reservationService = reservationService;
        _userService = userService;
        _clinicService = clinicService;
        _patientService = patientService;
        _paymentService = paymentService;
    }

    public async Task<IActionResult> Index(
        [FromQuery] ReservationFilterRequest filter,
        [FromQuery] PaginationRequest pagination)
    {
        ViewBag.Filter = filter;
        ViewBag.Pagination = pagination;
        // Search-first: honor selected filters/deep links, but never query the unfiltered index.
        var hasFilter = filter.DateFrom.HasValue || filter.DateTo.HasValue
            || !string.IsNullOrWhiteSpace(filter.Category) || filter.DoctorId.HasValue
            || filter.PatientId.HasValue || filter.ClinicId.HasValue
            || filter.StatusId.HasValue || filter.IsPaid.HasValue;
        ViewBag.HasSearch = hasFilter;

        // Reject invalid criteria instead of letting the service silently ignore an unknown category.
        if (!string.IsNullOrWhiteSpace(filter.Category)
            && (!Enum.TryParse<SafyaClinic.Domain.Enums.TreatmentCategory>(filter.Category, out var category)
                || !Enum.IsDefined(category)))
            ModelState.AddModelError(nameof(filter.Category), "Select a valid treatment category.");
        if (filter.DateFrom.HasValue && filter.DateTo.HasValue && filter.DateFrom > filter.DateTo)
            ModelState.AddModelError(nameof(filter.DateTo), "The end date must be on or after the start date.");
        if (!ModelState.IsValid || !hasFilter)
            return View(new PagedResult<ReservationSummaryDto> { Page = 1, PageSize = pagination.PageSize });

        var result = await _reservationService.GetReservationsAsync(filter, pagination);
        // A service failure should not be presented as a successful search with no matches.
        if (!result.IsSuccess)
            ModelState.AddModelError("", "Could not load reservations. Please try the filters again.");
        return View(result.IsSuccess ? result.Data : null);
    }

    public IActionResult Today([FromQuery] int? doctorId)
    {
        // P2d: today's list is the Appointments day (bookings, coverage and free times in one place).
        return RedirectToAction("Index", "Appointments");
    }

    [HttpGet]
    [RequirePermission(Permissions.Reservations.ViewQueue)]
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

        var canViewAllQueues = await CanAsync(Permissions.Reservations.ViewAllQueues);

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
            NotArrived = await _reservationService.GetExpectedArrivalsAsync(clinicId, doctorId),

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
    public IActionResult Board(
    DateTime? date,
    int? doctorId,
    int? clinicId)
    {
        // P2d: the appointment board is replaced by the Appointments screen (same day and clinic).
        return RedirectToAction("Index", "Appointments", new { date = date?.ToString("yyyy-MM-dd"), clinicId });
    }
    public async Task<IActionResult> Details(int id)
    {
        var result = await _reservationService.GetReservationByIdAsync(id);
        if (!result.IsSuccess) { Error("Reservation not found."); return RedirectToAction(nameof(Index)); }
        ViewBag.CanCollectPayment = false;
        ViewBag.CollectionStateKnown = false;
        if (await CanAsync(Permissions.Payments.Collect) && result.Data is { } reservation)
        {
            var due = await _paymentService.GetDueAmountAsync(
                reservation.PatientId, reservation.Id, null);
            ViewBag.CollectionStateKnown = due.IsSuccess;
            ViewBag.CanCollectPayment = due.IsSuccess && due.Data > 0m;
        }

        return View(result.Data);
    }

    [HttpGet]
    [RequirePermission(Permissions.Reservations.Manage)]
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
    [RequirePermission(Permissions.Reservations.Manage)]
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
    [RequirePermission(Permissions.Reservations.Manage)]
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
    [RequirePermission(Permissions.Reservations.Manage)]
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
        var result = await _reservationService.UpdateReservationAsync(id, model, CurrentUserId);
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
    [RequirePermission(Permissions.Reservations.Manage)]
    public async Task<IActionResult> UpdateStatus(int id, int statusId)
    {
        var result = await _reservationService.UpdateStatusAsync(
            id,
            statusId,
            CurrentUserId);

        if (!result.IsSuccess)
        {
            Error(result.Errors.FirstOrDefault() ?? "Could not update reservation status.");
            return RedirectToAction(nameof(Details), new { id });
        }

        if (result.Data is { StatusName: "Completed" } r && await CanAsync(Permissions.Payments.Collect))
        {
            var due = await _paymentService.GetDueAmountAsync(r.PatientId, id, null);
            if (due.IsSuccess && due.Data > 0m)
                return RedirectToAction("Collect", "Payments",
                    new { patientId = r.PatientId, reservationId = id });
            if (!due.IsSuccess)
                Error("Reservation completed, but collection eligibility could not be confirmed. Check the payment summary.");
        }


        return RedirectToAction(nameof(Details), new { id });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.Reservations.Manage)]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        var result = await _reservationService.CancelReservationAsync(id, CurrentUserId, reason);

        if (!result.IsSuccess)
        {
            Error(result.Errors.FirstOrDefault()
                ?? "Could not cancel the reservation.");

            return RedirectToAction(nameof(Details), new { id });
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
    [RequirePermission(Permissions.Reservations.CheckIn)]
    public async Task<IActionResult> CheckIn(int id, string? returnUrl = null)
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

        // P4: checking in from the Appointments day returns there; never to another site.
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(nameof(Details), new { id });
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.Reservations.StartConsultation)]
    public async Task<IActionResult> StartConsultation(int id)
    {
        var result = await _reservationService.StartConsultationAsync(
            id,
            CurrentUserId,
            await CanAsync(Permissions.Consultations.ActOnBehalf));

        if (!result.IsSuccess)
        {
            Error(result.Errors.FirstOrDefault()
                ?? "Could not start consultation.");

            return RedirectToAction(nameof(Details), new { id });
        }

        Success(result.Message);

        return RedirectToAction(
            "Consultation",
            "MedicalRecords",
            new { reservationId = id });
    }
}
