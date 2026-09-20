using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.MedicalRecord;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Web.Models;
using SafyaClinic.Application.DTOs.Reservation;

namespace SafyaClinic.Web.Controllers;

[Authorize(Roles = "Admin,Reception,Doctor")]
public class FollowUpsController : BaseController
{
    private readonly IPatientRecordService _recordService;
    private readonly IUserService _userService;
    private readonly IReservationService _reservationService;
    private readonly IClinicService _clinicService;

    public FollowUpsController(
        IPatientRecordService recordService,
        IUserService userService,
        IReservationService reservationService,
        IClinicService clinicService)
    {
        _recordService = recordService;
        _userService = userService;
        _reservationService = reservationService;
        _clinicService = clinicService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] PatientFollowUpFilter filter,
        [FromQuery] PaginationRequest pagination)
    {
        if (CurrentUserId <= 0)
            return Challenge();

        var canViewAll = IsAdmin || IsReception;
        var doctors = new List<SelectListItem>();

        if (canViewAll)
        {
            var doctorsResult = await _userService.GetDoctorsAsync();

            if (!doctorsResult.IsSuccess || doctorsResult.Data is null)
            {
                Error("Could not load the doctor filter.");
                return RedirectToAction("Index", "Dashboard");
            }

            doctors = doctorsResult.Data
                .OrderBy(d => d.FullName)
                .Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = d.FullName
                })
                .ToList();
        }

        var results = new PagedResult<PatientFollowUpDto>
        {
            Page = 1,
            PageSize = pagination.PageSize
        };

        if (ModelState.IsValid)
        {
            var result = await _recordService.GetPendingFollowUpsAsync(
                filter,
                pagination,
                CurrentUserId,
                canViewAll);

            if (result.IsSuccess && result.Data is not null)
                results = result.Data;
            else
                ApplyErrors(result);
        }

        return View(new PatientFollowUpsViewModel
        {
            DoctorId = canViewAll ? filter.DoctorId : CurrentUserId,
            Due = filter.Due,
            From = filter.From,
            To = filter.To,
            CanViewAll = canViewAll,
            Doctors = doctors,
            Results = results
        });
    }
    [HttpGet]
    public async Task<IActionResult> Book(int recordId)
    {
        if (CurrentUserId <= 0)
            return Challenge();

        if (!ModelState.IsValid)
            return BadRequest("Invalid follow-up.");

        var model = new BookFollowUpViewModel { RecordId = recordId };

        if (!await LoadBookingFormAsync(model))
            return RedirectToAction(nameof(Index));

        model.ReservationDate = model.Context!.FollowUpDate.Date < DateTime.Today
            ? DateTime.Today
            : model.Context.FollowUpDate.Date;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(BookFollowUpViewModel model)
    {
        if (CurrentUserId <= 0)
            return Challenge();

        if (!await LoadBookingFormAsync(model))
            return RedirectToAction(nameof(Index));

        if (!model.Clinics.Any(c => c.Value == model.ClinicId.ToString()))
            ModelState.AddModelError(nameof(model.ClinicId), "Select an available clinic.");

        if (!model.Treatments.Any(t => t.Value == model.TreatmentTypeId.ToString()))
            ModelState.AddModelError(nameof(model.TreatmentTypeId), "Select an available treatment.");

        if (model.ReservationTime.HasValue &&
            (model.ReservationTime.Value < TimeSpan.Zero ||
             model.ReservationTime.Value >= TimeSpan.FromDays(1)))
        {
            ModelState.AddModelError(
                nameof(model.ReservationTime), "Enter a valid appointment time.");
        }

        if (!ModelState.IsValid)
            return View(model);

        var typesResult = await _reservationService.GetTreatmentTypesAsync(
            model.Context!.Category);

        if (!typesResult.IsSuccess || typesResult.Data is null)
        {
            ModelState.AddModelError("", "Could not load treatment details.");
            return View(model);
        }

        var treatment = typesResult.Data.FirstOrDefault(
            t => t.Id == model.TreatmentTypeId);

        if (treatment is null)
        {
            ModelState.AddModelError(
                nameof(model.TreatmentTypeId), "The treatment is no longer available.");
            return View(model);
        }

        var result = await _reservationService.BookFollowUpAsync(
            model.RecordId,
            new CreateReservationRequest
            {
                PatientId = model.Context.PatientId,
                DoctorId = model.Context.DoctorId,
                Category = model.Context.Category,
                ClinicId = model.ClinicId,
                TreatmentTypeId = model.TreatmentTypeId,
                ReservationDate = model.ReservationDate!.Value.Date,
                ReservationTime = model.ReservationTime!.Value,
                DurationMinutes = treatment.DurationMinutes,
                Reason = "Follow-up",
                Notes = model.Notes,
                TotalAmount = null
            },
            CurrentUserId,
            IsAdmin || IsReception);

        if (!result.IsSuccess || result.Data is null)
        {
            ApplyErrors(result);
            return View(model);
        }

        Success("Follow-up booked.");

        return RedirectToAction(
            "Details",
            "Reservations",
            new { id = result.Data.Id });
    }

    private async Task<bool> LoadBookingFormAsync(BookFollowUpViewModel model)
    {
        var contextResult = await _recordService.GetFollowUpBookingContextAsync(
            model.RecordId,
            CurrentUserId,
            IsAdmin || IsReception);

        if (!contextResult.IsSuccess || contextResult.Data is null)
        {
            Error(contextResult.Errors.FirstOrDefault()
                ?? "Could not load the follow-up.");
            return false;
        }

        model.Context = contextResult.Data;

        var clinics = await _clinicService.GetAllAsync(includeInactive: false);
        var treatments = await _reservationService.GetTreatmentTypesAsync(
            model.Context.Category);

        if (!clinics.IsSuccess || clinics.Data is null ||
            !treatments.IsSuccess || treatments.Data is null)
        {
            Error("Could not load booking options.");
            return false;
        }

        model.Clinics = clinics.Data
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = c.Name
            })
            .ToList();

        model.Treatments = treatments.Data
            .Select(t => new SelectListItem
            {
                Value = t.Id.ToString(),
                Text = t.TypeName +
                    (t.DefaultCost.HasValue ? $" — {t.DefaultCost.Value:C}" : "")
            })
            .ToList();

        return true;
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(int recordId, string? reason)
    {
        if (CurrentUserId <= 0)
            return Challenge();

        if (!ModelState.IsValid)
        {
            Error("Invalid dismissal request.");
            return RedirectToAction(nameof(Index));
        }

        var result = await _recordService.DismissFollowUpAsync(
            recordId,
            reason,
            CurrentUserId,
            IsAdmin || IsReception);

        if (result.IsSuccess)
            Success(result.Message);
        else
            Error(result.Errors.FirstOrDefault()
                ?? "Could not dismiss the follow-up.");

        return RedirectToAction(nameof(Index));
    }
}