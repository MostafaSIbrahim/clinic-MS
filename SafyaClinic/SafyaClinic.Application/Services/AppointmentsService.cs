using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Common;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.Scheduling;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Interfaces.Repositories;

namespace SafyaClinic.Application.Services;

/// <summary>P2b: builds the appointments screen from the calendars (P1) and the reservations.</summary>
public class AppointmentsService : IAppointmentsService
{
    private readonly IUnitOfWork _uow;
    private readonly IClinicClock _clock;

    public AppointmentsService(IUnitOfWork uow, IClinicClock? clock = null)
    {
        _uow = uow;
        _clock = clock ?? new ClinicClock(ClinicClock.FindZone(ClinicClock.DefaultTimeZoneId));
    }

    public async Task<ServiceResult<AppointmentDayDto>> GetDayAsync(DateTime date, int clinicId, int visitMinutes)
    {
        date = date.Date;
        var clinic = await _uow.Clinics.Query().Where(c => c.Id == clinicId && c.IsActive).Select(c => c.Name).FirstOrDefaultAsync();
        if (clinic is null)
            return ServiceResult<AppointmentDayDto>.Failure("Clinic not found.");

        var hours = await CalendarData.ClinicHoursAsync(_uow, clinicId);
        var doctors = await _uow.Users.Query()
            .Where(u => u.IsActive && u.ClinicianType != ClinicianType.None)
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync();

        // Everything that holds a time that day, in every clinic (a doctor can't be in two places).
        var holding = await CalendarData.BookingsAsync(_uow, date, doctorId: null, clinicId: null, toDate: date);

        // What this clinic's day shows: its reservations except cancelled ones, with the patient's name.
        var shown = await _uow.Reservations.Query()
            .Where(r => r.ClinicId == clinicId && r.ReservationDate == date && r.Status.StatusName != "Cancelled")
            .Select(r => new
            {
                r.DoctorId,
                Booking = new AppointmentDto(r.Id, r.ReservationTime, r.DurationMinutes,
                    r.Patient.FirstName + " " + r.Patient.LastName, r.Status.StatusName)
                {
                    PatientId = r.PatientId,
                    IsPaid = r.IsPaid,
                },
            })
            .ToListAsync();

        var columns = new List<DoctorDayDto>();
        var notHere = new List<string>();
        foreach (var doctor in doctors)
        {
            var (schedule, changes) = await CalendarData.ScheduleAsync(_uow, doctor.Id, clinicId, date);
            var inputs = new CalendarInputs(hours, schedule, changes);
            var periods = Availability.For(date, inputs).Periods;
            var bookings = shown.Where(s => s.DoctorId == doctor.Id).Select(s => s.Booking).OrderBy(b => b.Start).ToList();

            // Shown when bookable here that day (the same rule the server applies) or already booked here.
            if (periods.Count == 0 && bookings.Count == 0)
            {
                notHere.Add(doctor.FullName);
                continue;
            }

            var free = Availability.FreeStarts(date, visitMinutes, inputs, holding.Where(b => b.DoctorId == doctor.Id), _clock.Now);
            columns.Add(new DoctorDayDto(doctor.Id, doctor.FullName, schedule is not null, periods, bookings, free));
        }

        return ServiceResult<AppointmentDayDto>.Success(
            new AppointmentDayDto(date, clinicId, clinic, hours.Count > 0, visitMinutes, columns, notHere));
    }

    public async Task<ServiceResult<MoveOptionsDto>> GetMoveAsync(int reservationId, DateTime? date, int? clinicId)
    {
        var r = await _uow.Reservations.Query()
            .Where(x => x.Id == reservationId)
            .Select(x => new
            {
                x.DoctorId, DoctorName = x.Doctor.FullName, x.ClinicId, ClinicName = x.Clinic.Name,
                x.ReservationDate, x.ReservationTime, x.DurationMinutes, x.QueueStatus, Status = x.Status.StatusName,
                PatientName = x.Patient.FirstName + " " + x.Patient.LastName,
            })
            .FirstOrDefaultAsync();
        if (r is null)
            return ServiceResult<MoveOptionsDto>.Failure("Reservation not found.");

        var notMovable = r.Status is "Completed" or "Cancelled" or "NoShow" || r.QueueStatus != PatientQueueStatus.NotCheckedIn
            ? "Only upcoming bookings that haven't been checked in can be moved."
            : null;

        var targetDate = (date ?? r.ReservationDate).Date;
        var targetClinicId = clinicId ?? r.ClinicId;
        var targetClinic = await _uow.Clinics.Query().Where(c => c.Id == targetClinicId && c.IsActive).Select(c => c.Name).FirstOrDefaultAsync();
        if (targetClinic is null)
            return ServiceResult<MoveOptionsDto>.Failure("Clinic not found.");

        IReadOnlyList<TimeSpan> free = Array.Empty<TimeSpan>();
        if (notMovable is null)
        {
            var inputs = await CalendarData.InputsAsync(_uow, r.DoctorId, targetClinicId, targetDate);
            var bookings = (await CalendarData.BookingsAsync(_uow, targetDate, r.DoctorId, clinicId: null, toDate: targetDate))
                .Where(b => b.ReservationId != reservationId);
            free = Availability.FreeStarts(targetDate, r.DurationMinutes, inputs, bookings, _clock.Now)
                .Where(t => !(targetDate == r.ReservationDate.Date && targetClinicId == r.ClinicId && t == r.ReservationTime))
                .ToList();
        }

        return ServiceResult<MoveOptionsDto>.Success(new MoveOptionsDto(
            reservationId, r.PatientName, r.DoctorId, r.DoctorName,
            r.ClinicId, r.ClinicName, r.ReservationDate.Date, r.ReservationTime, r.DurationMinutes,
            targetDate, targetClinicId, targetClinic, free, notMovable));
    }
}
