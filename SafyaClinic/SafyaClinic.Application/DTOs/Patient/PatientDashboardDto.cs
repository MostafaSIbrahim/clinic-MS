using SafyaClinic.Application.DTOs.Reservation;

namespace SafyaClinic.Application.DTOs.Patient;

public class PatientDashboardDto
{
    public PatientDto Patient { get; init; } = new();

    public ReservationSummaryDto? LastCompletedVisit { get; init; }

    public ReservationSummaryDto? NextAppointment { get; init; }
}