using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Settings;
using SafyaClinic.Application.Interfaces.Services;

namespace SafyaClinic.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public class PatientSourcesController : BaseController
{
    private readonly IPatientSourceService _sourceService;

    public PatientSourcesController(IPatientSourceService sourceService) =>
        _sourceService = sourceService;

    public async Task<IActionResult> Index()
    {
        var result = await _sourceService.GetAllAsync(includeInactive: true);
        return View(result.IsSuccess ? result.Data : Enumerable.Empty<PatientSourceDto>());
    }

    [HttpGet]
    public IActionResult Create() => View(new CreatePatientSourceRequest());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePatientSourceRequest model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await _sourceService.CreateAsync(model);
        if (!result.IsSuccess) { ApplyErrors(result); return View(model); }
        return RedirectWithSuccess("Patient source created.", nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _sourceService.GetByIdAsync(id);
        if (!result.IsSuccess) { Error("Source not found."); return RedirectToAction(nameof(Index)); }
        var s = result.Data!;
        ViewBag.SourceId = id;
        return View(new UpdatePatientSourceRequest
        {
            Name = s.Name,
            Description = s.Description,
            DefaultDeductionPercentage = s.DefaultDeductionPercentage,
            IsActive = s.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdatePatientSourceRequest model)
    {
        if (!ModelState.IsValid) { ViewBag.SourceId = id; return View(model); }
        var result = await _sourceService.UpdateAsync(id, model);
        if (!result.IsSuccess) { ApplyErrors(result); ViewBag.SourceId = id; return View(model); }
        return RedirectWithSuccess("Patient source updated.", nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _sourceService.DeleteAsync(id);
        if (!result.IsSuccess) Error(result.Errors.FirstOrDefault() ?? "Could not delete source.");
        else Success(result.Message);
        return RedirectToAction(nameof(Index));
    }
}
