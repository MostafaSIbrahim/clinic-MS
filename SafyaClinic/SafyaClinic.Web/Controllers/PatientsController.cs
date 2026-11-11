using SafyaClinic.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Web.Security;
using SafyaClinic.Application.Security;

namespace SafyaClinic.Web.Controllers;

[RequirePermission(Permissions.Patients.View)]
public class PatientsController : BaseController
{
    private readonly IPatientService _patientService;
    private readonly IPatientSourceService _patientSourceService;

    public PatientsController(IPatientService patientService, IPatientSourceService patientSourceService)
    {
        _patientService = patientService;
        _patientSourceService = patientSourceService;
    }

    // ── List / Search ─────────────────────────────────────────

    public async Task<IActionResult> Index([FromQuery] PaginationRequest request)
    {
        ViewBag.Search = request.Search;
        ViewBag.Page = request.Page;
        ViewBag.PageSize = request.PageSize;
        // Search-first: opening or clearing the index must not execute a patient list/count query.
        var hasSearch = !string.IsNullOrWhiteSpace(request.Search);
        ViewBag.HasSearch = hasSearch;
        if (!ModelState.IsValid || !hasSearch)
            return View(new PagedResult<PatientSummaryDto> { Page = 1, PageSize = request.PageSize });

        var result = await _patientService.SearchPatientsAsync(request);
        // Keep a failed search distinct from an empty set of matching patients.
        if (!result.IsSuccess)
            ModelState.AddModelError("", "Could not load patients. Please try the search again.");

        // P3: one match (e.g. a phone number from the top bar) opens that patient straight away.
        if (result.IsSuccess && result.Data is { TotalCount: 1 } one && request.Page == 1 && one.Items.FirstOrDefault() is { } only)
            return RedirectToAction(nameof(Details), new { id = only.Id });

        return View(result.IsSuccess ? result.Data : null);
    }

    // ── Detail ────────────────────────────────────────────────

    // M1: the patient workspace. One tab is loaded at a time (?tab=visits); tabs a person can't use are hidden.
    public async Task<IActionResult> Details(int id, string? tab = null, int page = 1,
        [FromServices] IReservationService? reservations = null,
        [FromServices] IPatientRecordService? records = null,
        [FromServices] IAnalysisService? analyses = null,
        [FromServices] INutritionService? nutrition = null,
        [FromServices] IPaymentService? payments = null)
    {
        var result = await _patientService.GetPatientDashboardAsync(id);

        if (!result.IsSuccess || result.Data is null)
        {
            Error("Patient not found.");
            return RedirectToAction(nameof(Index));
        }

        var tabs = await PatientTabs.VisibleAsync(CanAsync);
        var active = await PatientTabs.ChooseAsync(tab, tabs, CanAsync);

        PagedResult<SafyaClinic.Application.DTOs.Reservation.ReservationSummaryDto>? visits = null;
        if (active == PatientTabs.Visits && reservations is not null)
        {
            var found = await reservations.GetReservationsAsync(
                new SafyaClinic.Application.DTOs.Reservation.ReservationFilterRequest { PatientId = id },
                new PaginationRequest { Page = Math.Max(1, page), PageSize = 20 });
            if (found.IsSuccess) visits = found.Data;
        }

        // M1b: the clinical tabs. A tab is only chosen when the person may see it (above).
        IReadOnlyList<SafyaClinic.Application.DTOs.MedicalRecord.PatientRecordDto>? recordList = null;
        if (active == PatientTabs.Records && records is not null)
        {
            var found = await records.GetPatientRecordsAsync(id);
            recordList = found.IsSuccess && found.Data is not null
                ? found.Data.OrderByDescending(r => r.CreatedAt).ToList()
                : new List<SafyaClinic.Application.DTOs.MedicalRecord.PatientRecordDto>();
        }

        IReadOnlyList<SafyaClinic.Application.DTOs.Analysis.MedicalAnalysisDto>? labList = null;
        if (active == PatientTabs.Lab && analyses is not null)
        {
            var found = await analyses.GetPatientAnalysesAsync(id);
            labList = found.IsSuccess && found.Data is not null
                ? found.Data.ToList()
                : new List<SafyaClinic.Application.DTOs.Analysis.MedicalAnalysisDto>();
        }

        // M1c: nutrition and payments.
        IReadOnlyList<SafyaClinic.Application.DTOs.Nutrition.PatientEnrollmentDto>? enrollments = null;
        if (active == PatientTabs.Nutrition && nutrition is not null)
        {
            var found = await nutrition.GetPatientEnrollmentsAsync(id);
            enrollments = found.IsSuccess && found.Data is not null
                ? found.Data.ToList()
                : new List<SafyaClinic.Application.DTOs.Nutrition.PatientEnrollmentDto>();
        }

        SafyaClinic.Application.DTOs.Payment.PatientFinancialSummaryDto? money = null;
        if (active == PatientTabs.Payments && payments is not null)
        {
            var found = await payments.GetPatientFinancialSummaryAsync(id);
            if (found.IsSuccess) money = found.Data;
        }

        // M1d: the timeline.
        PagedResult<PatientTimelineItemDto>? timeline = null;
        if (active == PatientTabs.Timeline)
        {
            var found = await _patientService.GetPatientTimelineAsync(id, new PaginationRequest { Page = Math.Max(1, page), PageSize = 20 });
            if (found.IsSuccess) timeline = found.Data;
        }

        return View(new PatientWorkspaceModel
        {
            Dashboard = result.Data, Tabs = tabs, ActiveTab = active, Visits = visits, Records = recordList, Lab = labList,
            Enrollments = enrollments, Payments = money, Timeline = timeline
        });
    }

    [HttpGet]
    [RequirePermission(Permissions.Patients.ViewTimeline)]
    // M1d: the timeline is the Timeline tab of the patient workspace; old links land there.
    public IActionResult Timeline(int id, [FromQuery] PaginationRequest pagination) =>
        RedirectToAction(nameof(Details), new { id, tab = PatientTabs.Timeline, page = pagination.Page });

    // ── Create ────────────────────────────────────────────────

    [HttpGet]
    [RequirePermission(Permissions.Patients.Edit)]
    public async Task<IActionResult> Create(bool popup = false)
    {
        ViewBag.Sources = (await _patientSourceService.GetAllAsync(includeInactive: false)).Data;
        ViewBag.Popup = popup;
        return View(new CreatePatientRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.Patients.Edit)]
    public async Task<IActionResult> Create(CreatePatientRequest model, bool popup = false)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Sources = (await _patientSourceService.GetAllAsync(includeInactive: false)).Data;
            ViewBag.Popup = popup;
            return View(model);
        }
        var result = await _patientService.CreatePatientAsync(model, CurrentUserId);
        if (!result.IsSuccess)
        {
            ApplyErrors(result);
            ViewBag.DuplicateOfPatientId = DuplicatePatient.FindId(result.Errors);   // P3: offer the existing patient
            ViewBag.Sources = (await _patientSourceService.GetAllAsync(includeInactive: false)).Data;
            ViewBag.Popup = popup;
            return View(model);
        }
        if (popup)
        {
            var patient = result.Data!;
            var primaryPhone = patient.Phones.FirstOrDefault(p => p.IsPrimary)?.PhoneNumber
                ?? patient.Phones.FirstOrDefault()?.PhoneNumber;
            ViewBag.NewPatientId = patient.Id;
            ViewBag.NewPatientName = patient.FullName;
            ViewBag.NewPatientPhone = primaryPhone ?? "";
            return View("CreateSuccessPopup");
        }
        return RedirectWithSuccess("Patient registered.", nameof(Details), routeValues: new { id = result.Data!.Id });
    }

    // ── Use an existing patient from the booking pop-up (P3) ──

    [HttpGet]
    [RequirePermission(Permissions.Patients.View)]
    public async Task<IActionResult> UseExisting(int id)
    {
        var result = await _patientService.GetPatientByIdAsync(id);
        if (!result.IsSuccess || result.Data is null) return NotFound();
        var patient = result.Data;
        ViewBag.NewPatientId = patient.Id;
        ViewBag.NewPatientName = patient.FullName;
        ViewBag.NewPatientPhone = (patient.Phones.FirstOrDefault(p => p.IsPrimary) ?? patient.Phones.FirstOrDefault())?.PhoneNumber ?? "";
        ViewBag.ExistingPatient = true;
        return View("CreateSuccessPopup");
    }

    // ── Edit Basic (Reception + above) ───────────────────────

    [HttpGet]
    [RequirePermission(Permissions.Patients.Edit)]
    public async Task<IActionResult> EditBasic(int id)
    {
        var result = await _patientService.GetPatientByIdAsync(id);
        if (!result.IsSuccess) return RedirectToAction(nameof(Index));
        var p = result.Data!;
        ViewBag.Sources = (await _patientSourceService.GetAllAsync(includeInactive: false)).Data;
        return View(new UpdatePatientBasicRequest
        {
            FirstName = p.FirstName,
            LastName = p.LastName,
            NationalId = p.NationalId,
            PatientSourceId = p.PatientSourceId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.Patients.Edit)]
    public async Task<IActionResult> EditBasic(int id, UpdatePatientBasicRequest model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Sources = (await _patientSourceService.GetAllAsync(includeInactive: false)).Data;
            return View(model);
        }
        var result = await _patientService.UpdateBasicInfoAsync(id, model);
        if (!result.IsSuccess) { ApplyErrors(result); return View(model); }
        return RedirectWithSuccess("Patient updated.", nameof(Details), routeValues: new { id });
    }

    // ── Edit Medical (Doctor + Admin) ─────────────────────────

    [HttpGet]
    [RequirePermission(Permissions.Patients.EditMedical)]
    public async Task<IActionResult> EditMedical(int id)
    {
        var result = await _patientService.GetPatientByIdAsync(id);
        if (!result.IsSuccess) return RedirectToAction(nameof(Index));
        var p = result.Data!;
        return View(new UpdatePatientMedicalRequest
        {
            DateOfBirth = p.DateOfBirth,
            Gender = p.Gender,
            BloodType = p.BloodType,
            HeightCm = p.HeightCm,
            Weight = p.Weight,
            Allergies = p.Allergies,
            ChronicDiseases = p.ChronicDiseases,
            Notes = p.Notes
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.Patients.EditMedical)]
    public async Task<IActionResult> EditMedical(int id, UpdatePatientMedicalRequest model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _patientService.UpdateMedicalInfoAsync(id, model);
        if (!result.IsSuccess) { ApplyErrors(result); return View(model); }
        return RedirectWithSuccess("Medical info updated.", nameof(Details), routeValues: new { id });
    }

    // ── Phones ────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.Patients.Edit)]
    public async Task<IActionResult> AddPhone(
    int patientId,
    CreatePatientPhoneRequest model)
    {
        var isAjax =
            Request.Headers["X-Requested-With"].ToString() == "XMLHttpRequest";

        var number = model.PhoneNumber?.Trim();

        if (patientId <= 0)
            ModelState.AddModelError("", "Invalid patient.");

        if (string.IsNullOrWhiteSpace(number) || number.Length > 20)
        {
            ModelState.AddModelError(
                nameof(model.PhoneNumber),
                "Enter a phone number of up to 20 characters.");
        }

        if (model.PhoneType is not ("Mobile" or "Home" or "Work"))
            ModelState.AddModelError(nameof(model.PhoneType), "Select a valid phone type.");

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
                    ? "Check the entered values."
                    : e.ErrorMessage)
                .ToArray();

            if (isAjax)
                return BadRequest(new { errors });

            Error(string.Join(" ", errors));
            return RedirectToAction(nameof(Details), new { id = patientId });
        }

        var result = await _patientService.AddPhoneAsync(patientId, model);

        if (!result.IsSuccess)
        {
            if (isAjax)
                return BadRequest(new { errors = result.Errors });

            Error(result.Errors.FirstOrDefault() ?? "Could not add the phone.");
            return RedirectToAction(nameof(Details), new { id = patientId });
        }

        Success("Phone added.");

        if (isAjax)
            return Json(new { success = true });

        return RedirectToAction(nameof(Details), new { id = patientId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.Patients.Edit)]
    public async Task<IActionResult> RemovePhone(int patientId, int phoneId)
    {
        var result = await _patientService.RemovePhoneAsync(patientId, phoneId);
        if (!result.IsSuccess)
            Error(result.Errors.FirstOrDefault() ?? "Could not remove the phone.");
        else
            Success("Phone removed.");

        return RedirectToAction(nameof(Details), new { id = patientId });
    }

    // ── Addresses ─────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.Patients.Edit)]
    public async Task<IActionResult> AddAddress(
    int patientId, CreatePatientAddressRequest model)
    {
        var isAjax = Request.Headers["X-Requested-With"].ToString() == "XMLHttpRequest";
        model = new CreatePatientAddressRequest
        {
            City = model.City?.Trim() ?? "",
            Street = model.Street?.Trim(),
            Governorate = model.Governorate?.Trim(),
            PostalCode = model.PostalCode?.Trim(),
            IsPrimary = model.IsPrimary
        };

        if (patientId <= 0)
            ModelState.AddModelError("", "Invalid patient.");
        if (string.IsNullOrWhiteSpace(model.City) || model.City.Length > 50)
            ModelState.AddModelError(nameof(model.City), "Enter a city of up to 50 characters.");
        if (model.Street?.Length > 200)
            ModelState.AddModelError(nameof(model.Street), "Street must be 200 characters or fewer.");
        if (model.Governorate?.Length > 50)
            ModelState.AddModelError(nameof(model.Governorate), "Governorate must be 50 characters or fewer.");
        if (model.PostalCode?.Length > 10)
            ModelState.AddModelError(nameof(model.PostalCode), "Postal code must be 10 characters or fewer.");

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
                    ? "Check the entered values." : e.ErrorMessage).ToArray();
            if (isAjax) return BadRequest(new { errors });
            Error(string.Join(" ", errors));
            return RedirectToAction(nameof(Details), new { id = patientId });
        }

        var result = await _patientService.AddAddressAsync(patientId, model);
        if (!result.IsSuccess)
        {
            if (isAjax) return BadRequest(new { errors = result.Errors });
            Error(result.Errors.FirstOrDefault() ?? "Could not add the address.");
            return RedirectToAction(nameof(Details), new { id = patientId });
        }

        Success("Address added.");
        if (isAjax) return Json(new { success = true });
        return RedirectToAction(nameof(Details), new { id = patientId });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.Patients.Edit)]
    public async Task<IActionResult> RemoveAddress(int patientId, int addressId)
    {
        var result = await _patientService.RemoveAddressAsync(patientId, addressId);
        if (!result.IsSuccess)
            Error(result.Errors.FirstOrDefault() ?? "Could not remove the address.");
        else
            Success("Address removed.");

        return RedirectToAction(nameof(Details), new { id = patientId });
    }
}
