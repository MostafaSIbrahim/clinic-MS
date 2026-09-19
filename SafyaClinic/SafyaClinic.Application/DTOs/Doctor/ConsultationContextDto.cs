namespace SafyaClinic.Application.DTOs.Reservation;

public class ConsultationContextDto
{
    public int ReservationId { get; init; }
    public int PatientId { get; init; }
    public int DoctorId { get; init; }
    public string Category { get; init; } = string.Empty;
    public int? PatientRecordId { get; init; }
}