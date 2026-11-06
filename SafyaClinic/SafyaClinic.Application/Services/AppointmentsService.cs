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
                    r.Patient.FirstName + " " + r.Patient.LastName, r.Status.StatusName),
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
}
