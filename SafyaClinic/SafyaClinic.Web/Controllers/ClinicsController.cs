using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Settings;
using SafyaClinic.Application.Interfaces.Services;

namespace SafyaClinic.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class ClinicsController : BaseController
{
    private readonly IClinicService _clinicService;
    private readonly IPatientSourceService _sourceService;

    public ClinicsController(IClinicService clinicService, IPatientSourceService sourceService)
    {
        _clinicService = clinicService;
        _sourceService = sourceService;
    }

    public async Task<IActionResult> Index()
    {
        var result = await _clinicService.GetAllAsync(includeInactive: true);
        return View(result.IsSuccess ? result.Data : Enumerable.Empty<ClinicDto>());
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateClinicRequest());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateClinicRequest model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _clinicService.CreateAsync(model);
        if (!result.IsSuccess) { ApplyErrors(result); return View(model); }
        return RedirectWithSuccess("Clinic created.", nameof(Details), routeValues: new { id = result.Data!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _clinicService.GetByIdAsync(id);
        if (!result.IsSuccess) { Error("Clinic not found."); return RedirectToAction(nameof(Index)); }
        var c = result.Data!;
        ViewBag.ClinicId = id;
        return View(new UpdateClinicRequest
        {
            Name = c.Name,
            Address = c.Address,
            Phone = c.Phone,
            IsActive = c.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateClinicRequest model)
    {
        if (!ModelState.IsValid) { ViewBag.ClinicId = id; return View(model); }
        var result = await _clinicService.UpdateAsync(id, model);
        if (!result.IsSuccess) { ApplyErrors(result); ViewBag.ClinicId = id; return View(model); }
        return RedirectWithSuccess("Clinic updated.", nameof(Details), routeValues: new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _clinicService.DeleteAsync(id);
        if (!result.IsSuccess) Error(result.Errors.FirstOrDefault() ?? "Could not delete clinic.");
        else Success(result.Message);
        return RedirectToAction(nameof(Index));
    }

    // ── Details + agreement matrix ────────────────────────────────

    public async Task<IActionResult> Details(int id)
    {
        var clinicResult = await _clinicService.GetByIdAsync(id);
        if (!clinicResult.IsSuccess) { Error("Clinic not found."); return RedirectToAction(nameof(Index)); }

        var sourcesResult = await _sourceService.GetAllAsync(includeInactive: false);
        ViewBag.Sources = sourcesResult.Data ?? Enumerable.Empty<PatientSourceDto>();

        return View(clinicResult.Data);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAgreement(UpsertClinicSourceAgreementRequest model)
    {
        var result = await _clinicService.UpsertAgreementAsync(model);
        if (!result.IsSuccess) Error(result.Errors.FirstOrDefault() ?? "Could not save agreement.");
        else Success("Agreement saved.");
        return RedirectToAction(nameof(Details), new { id = model.ClinicId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAgreement(int agreementId, int clinicId)
    {
        var result = await _clinicService.RemoveAgreementAsync(agreementId);
        if (!result.IsSuccess) Error(result.Errors.FirstOrDefault() ?? "Could not remove agreement.");
        else Success("Agreement removed.");
        return RedirectToAction(nameof(Details), new { id = clinicId });
    }
}
