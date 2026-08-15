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
        var result = await _patientService.SearchPatientsAsync(request);
        ViewBag.Search = request.Search;
        ViewBag.Page = request.Page;
        ViewBag.PageSize = request.PageSize;
        return View(result.IsSuccess ? result.Data : null);
    }

    // ── Detail ────────────────────────────────────────────────

    public async Task<IActionResult> Details(int id)
    {
        var result = await _patientService.GetPatientByIdAsync(id);
        if (!result.IsSuccess) { Error("Patient not found."); return RedirectToAction(nameof(Index)); }
        return View(result.Data);
    }

    // ── Create ────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Sources = (await _patientSourceService.GetAllAsync(includeInactive: false)).Data;
        return View(new CreatePatientRequest());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePatientRequest model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Sources = (await _patientSourceService.GetAllAsync(includeInactive: false)).Data;
            return View(model);
        }
        var result = await _patientService.CreatePatientAsync(model, CurrentUserId);
        if (!result.IsSuccess)
        {
            ApplyErrors(result);
            ViewBag.Sources = (await _patientSourceService.GetAllAsync(includeInactive: false)).Data;
            return View(model);
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
    public async Task<IActionResult> AddPhone(int patientId, CreatePatientPhoneRequest model)
    {
        var result = await _patientService.AddPhoneAsync(patientId, model);
        if (!result.IsSuccess) Error(result.Errors.First());
        return RedirectToAction(nameof(Details), new { id = patientId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePhone(int patientId, int phoneId)
    {
        await _patientService.RemovePhoneAsync(patientId, phoneId);
        return RedirectToAction(nameof(Details), new { id = patientId });
    }

    // ── Addresses ─────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAddress(int patientId, CreatePatientAddressRequest model)
    {
        var result = await _patientService.AddAddressAsync(patientId, model);
        if (!result.IsSuccess) Error(result.Errors.First());
        return RedirectToAction(nameof(Details), new { id = patientId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAddress(int patientId, int addressId)
    {
        await _patientService.RemoveAddressAsync(patientId, addressId);
        return RedirectToAction(nameof(Details), new { id = patientId });
    }
}