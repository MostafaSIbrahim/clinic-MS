using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Interfaces.Services;

namespace SafyaClinic.Web.Controllers;

[Authorize(Policy = "ClinicalStaff")]
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
        return View(result.IsSuccess ? result.Data : null);
    }

    // ── Detail ────────────────────────────────────────────────

    public async Task<IActionResult> Details(int id)
    {
        var result = await _patientService.GetPatientDashboardAsync(id);

        if (!result.IsSuccess)
        {
            Error("Patient not found.");
            return RedirectToAction(nameof(Index));
        }

        return View(result.Data);
    }

    [HttpGet]
    [Authorize(Policy = "DoctorOrAdmin")]
    public async Task<IActionResult> Timeline(
    int id,
    [FromQuery] PaginationRequest pagination)
    {
        var patientResult = await _patientService.GetPatientByIdAsync(id);

        if (!patientResult.IsSuccess || patientResult.Data is null)
            return NotFound("Patient not found.");

        var timelineResult =
            await _patientService.GetPatientTimelineAsync(id, pagination);

        if (!timelineResult.IsSuccess || timelineResult.Data is null)
        {
            Error("Could not load patient timeline.");
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(new PatientTimelineDto
        {
            PatientId = patientResult.Data.Id,
            PatientName = patientResult.Data.FullName,
            Timeline = timelineResult.Data
        });
    }

    // ── Create ────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Create(bool popup = false)
    {
        ViewBag.Sources = (await _patientSourceService.GetAllAsync(includeInactive: false)).Data;
        ViewBag.Popup = popup;
        return View(new CreatePatientRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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

    // ── Edit Basic (Reception + above) ───────────────────────

    [HttpGet]
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
    [Authorize(Policy = "DoctorOrAdmin")]
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
    [Authorize(Policy = "DoctorOrAdmin")]
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
