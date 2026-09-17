namespace SafyaClinic.Application.DTOs.Reservation;

public class AppointmentBoardDto
{
    public DateTime Date { get; init; }

    public int? DoctorId { get; init; }

    public int? ClinicId { get; init; }

    public IReadOnlyList<ReservationSummaryDto> Reservations { get; init; }
        = Array.Empty<ReservationSummaryDto>();
}