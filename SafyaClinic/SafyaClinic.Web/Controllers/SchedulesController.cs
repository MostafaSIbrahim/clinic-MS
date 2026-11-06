using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.Security;
using SafyaClinic.Web.Models;
using SafyaClinic.Web.Security;

namespace SafyaClinic.Web.Controllers;

/// <summary>
/// P1: doctors' calendars. Clinicians set their own (<c>Schedule.ManageOwn</c>); the front desk and
/// administrators set anyone's (<c>Schedule.ManageAll</c>). Calendars start limiting bookings in P2.
/// </summary>
[Authorize]
public class SchedulesController : BaseController
{
    private readonly IDoctorScheduleService _schedules;

    public SchedulesController(IDoctorScheduleService schedules) => _schedules = schedules;

    [HttpGet]
    [RequirePermission(Permissions.Schedule.ManageAll)]
    public async Task<IActionResult> Index()
    {
        var result = await _schedules.ListAsync();
        return View(result.Data ?? Array.Empty<SafyaClinic.Application.Scheduling.ScheduleSummaryDto>());
    }

    [HttpGet]
    [RequirePermission(Permissions.Schedule.ManageOwn)]
    public async Task<IActionResult> Mine()
    {
        var result = await _schedules.ListAsync(CurrentUserId);
        ViewBag.IsClinician = IsClinician;
        return View(result.Data ?? Array.Empty<SafyaClinic.Application.Scheduling.ScheduleSummaryDto>());
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int doctorId, int clinicId, DateTime? previewDate = null, int previewMinutes = 30)
    {
        if (!await MayEditAsync(doctorId)) return Forbid();

        var view = await _schedules.GetAsync(doctorId, clinicId);
        if (!view.IsSuccess) { Error(view.Errors.First()); return await BackAsync(); }
        ViewBag.Back = await BackActionAsync();
        ViewBag.DateChanges = view.Data!.DateChanges;
        ViewBag.Exists = view.Data.Exists;

        // P1c: free start times for a chosen date and visit length.
        if (previewDate is DateTime date)
        {
            previewMinutes = Math.Clamp(previewMinutes, 5, 240);
            var free = await _schedules.FreeStartsAsync(doctorId, clinicId, date, previewMinutes);
            ViewBag.PreviewDate = date.Date;
            ViewBag.PreviewMinutes = previewMinutes;
            ViewBag.FreeStarts = free.Data ?? Array.Empty<TimeSpan>();
        }
        return View(DoctorScheduleForm.From(view.Data));
    }

    // ── Date changes (P1c) ────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDate(int doctorId, int clinicId, DateChangeForm change)
    {
        if (!await MayEditAsync(doctorId)) return Forbid();

        var view = await _schedules.GetAsync(doctorId, clinicId);
        if (!view.IsSuccess) { Error(view.Errors.First()); return await BackAsync(); }

        var dto = change.ToChange(view.Data!.Schedule.Mode, out var formErrors);
        if (dto is null || formErrors.Count > 0)
        {
            Error(string.Join(" ", formErrors));
        }
        else
        {
            var result = await _schedules.SetDateChangeAsync(doctorId, clinicId, dto, CurrentUserId);
            if (result.IsSuccess) Success(result.Message);
            else { Error(string.Join(" ", result.Errors)); NoteStrandedBookings(result.Errors); }
        }
        return RedirectToAction(nameof(Edit), new { doctorId, clinicId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveDate(int doctorId, int clinicId, DateTime date)
    {
        if (!await MayEditAsync(doctorId)) return Forbid();

        var result = await _schedules.RemoveDateChangeAsync(doctorId, clinicId, date, CurrentUserId);
        if (result.IsSuccess) Success(result.Message);
        else { Error(string.Join(" ", result.Errors)); NoteStrandedBookings(result.Errors); }
        return RedirectToAction(nameof(Edit), new { doctorId, clinicId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int doctorId, int clinicId, DoctorScheduleForm form)
    {
        if (!await MayEditAsync(doctorId)) return Forbid();

        form.DoctorId = doctorId;
        form.ClinicId = clinicId;
        var schedule = form.ToSchedule(out var formErrors);
        foreach (var error in formErrors) ModelState.AddModelError(string.Empty, error);

        if (ModelState.IsValid)
        {
            var result = await _schedules.SaveAsync(schedule, CurrentUserId);
            if (result.IsSuccess)
            {
                Success(result.Message);
                return await BackAsync();
            }
            ApplyErrors(result);
            NoteStrandedBookings(result.Errors);
        }

        var view = await _schedules.GetAsync(doctorId, clinicId);
        if (!view.IsSuccess) { Error(view.Errors.First()); return await BackAsync(); }
        form.ShowDetails(view.Data!);
        ViewBag.Back = await BackActionAsync();
        ViewBag.DateChanges = view.Data!.DateChanges;
        ViewBag.Exists = view.Data.Exists;
        return View(form);
    }

    /// <summary>Anyone's calendar with ManageAll; one's own with ManageOwn.</summary>
    private async Task<bool> MayEditAsync(int doctorId) =>
        await CanAsync(Permissions.Schedule.ManageAll)
        || (doctorId == CurrentUserId && await CanAsync(Permissions.Schedule.ManageOwn));

    private async Task<string> BackActionAsync() =>
        await CanAsync(Permissions.Schedule.ManageAll) ? nameof(Index) : nameof(Mine);

    private async Task<IActionResult> BackAsync() => RedirectToAction(await BackActionAsync());
}
