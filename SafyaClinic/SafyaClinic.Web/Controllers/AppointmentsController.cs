using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.Common;
using SafyaClinic.Application.DTOs.Reservation;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.Security;
using SafyaClinic.Web.Models;
using SafyaClinic.Web.Security;

namespace SafyaClinic.Web.Controllers;

/// <summary>
/// P2b: the appointments screen. A clinic's day with each doctor's bookings and free times; clicking a free
/// time books it. The server re-checks the time (P2a), so a time taken meanwhile is refused with a message.
/// </summary>
[Authorize]
public class AppointmentsController : BaseController
{
    private const int DefaultVisitMinutes = 30;

    private readonly IAppointmentsService _appointments;
    private readonly IClinicService _clinics;
    private readonly IReservationService _reservations;
    private readonly IPatientService _patients;
    private readonly IUserService _users;
    private readonly IClinicClock _clock;

    public AppointmentsController(IAppointmentsService appointments, IClinicService clinics, IReservationService reservations,
        IPatientService patients, IUserService users, IClinicClock clock)
    {
        _appointments = appointments;
        _clinics = clinics;
        _reservations = reservations;
        _patients = patients;
        _users = users;
        _clock = clock;
    }

    [HttpGet]
    [RequirePermission(Permissions.Reservations.View)]
    public async Task<IActionResult> Index(DateTime? date, int? clinicId, int? treatmentTypeId)
    {
        var clinics = (await _clinics.GetAllAsync(includeInactive: false)).Data?.ToList() ?? new();
        var types = (await _reservations.GetTreatmentTypesAsync()).Data?.ToList() ?? new();
        var day = (date ?? _clock.Today).Date;
        var clinic = clinics.FirstOrDefault(c => c.Id == clinicId) ?? clinics.FirstOrDefault();
        var type = types.FirstOrDefault(t => t.Id == treatmentTypeId) ?? types.FirstOrDefault();

        var model = new AppointmentsPageModel
        {
            Clinics = clinics,
            TreatmentTypes = types,
            Date = day,
            ClinicId = clinic?.Id,
            TreatmentTypeId = type?.Id,
            CanBook = await CanAsync(Permissions.Reservations.Manage),
            Day = clinic is null ? null : (await _appointments.GetDayAsync(day, clinic.Id, VisitMinutes(type?.DurationMinutes))).Data,
        };
        return View(model);
    }

    [HttpGet]
    [RequirePermission(Permissions.Reservations.Manage)]
    public async Task<IActionResult> Book(int doctorId, int clinicId, DateTime date, TimeSpan time, int? treatmentTypeId, int? patientId)
    {
        var form = new BookAppointmentForm
        {
            DoctorId = doctorId,
            ClinicId = clinicId,
            Date = date.Date,
            Time = time,
            TreatmentTypeId = treatmentTypeId ?? 0,
            PatientId = patientId ?? 0,
        };
        return await ShowBookAsync(form) ?? RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(Permissions.Reservations.Manage)]
    public async Task<IActionResult> Book(BookAppointmentForm form)
    {
        var type = (await _reservations.GetTreatmentTypesAsync()).Data?.FirstOrDefault(t => t.Id == form.TreatmentTypeId);
        if (form.PatientId <= 0) ModelState.AddModelError(nameof(form.PatientId), "Choose a patient.");
        if (type is null) ModelState.AddModelError(nameof(form.TreatmentTypeId), "Choose a treatment type.");

        if (ModelState.IsValid)
        {
            var result = await _reservations.CreateReservationAsync(new CreateReservationRequest
            {
                PatientId = form.PatientId,
                DoctorId = form.DoctorId,
                ClinicId = form.ClinicId,
                TreatmentTypeId = type!.Id,
                Category = type.Category,
                ReservationDate = form.Date.Date,
                ReservationTime = form.Time,
                DurationMinutes = VisitMinutes(type.DurationMinutes),
                Reason = form.Reason,
                Notes = form.Notes,
                TotalAmount = null,   // the treatment type's price (D4)
            }, CurrentUserId);

            if (result.IsSuccess)
            {
                Success($"Booked {form.Time:hh\\:mm} on {form.Date:dd MMM yyyy}.");
                return RedirectToAction(nameof(Index), new { date = form.Date.ToString("yyyy-MM-dd"), clinicId = form.ClinicId, treatmentTypeId = type.Id });
            }
            ApplyErrors(result);
        }

        return await ShowBookAsync(form) ?? RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult?> ShowBookAsync(BookAppointmentForm form)
    {
        var doctor = (await _users.GetDoctorsAsync()).Data?.FirstOrDefault(d => d.Id == form.DoctorId);
        var clinic = (await _clinics.GetAllAsync(includeInactive: false)).Data?.FirstOrDefault(c => c.Id == form.ClinicId);
        if (doctor is null || clinic is null)
        {
            Error("Choose a free time on the appointments screen.");
            return null;
        }
        form.DoctorName = doctor.FullName;
        form.ClinicName = clinic.Name;
        if (form.PatientId > 0)
            form.PatientName = (await _patients.GetPatientByIdAsync(form.PatientId)).Data?.FullName;

        ViewBag.TreatmentTypes = (await _reservations.GetTreatmentTypesAsync()).Data?.ToList() ?? new();
        return View(form);
    }

    private static int VisitMinutes(int? treatmentMinutes) =>
        treatmentMinutes is > 0 ? treatmentMinutes.Value : DefaultVisitMinutes;
}
