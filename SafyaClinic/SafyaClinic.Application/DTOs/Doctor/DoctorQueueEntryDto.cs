using SafyaClinic.Domain.Enums;

namespace SafyaClinic.Application.DTOs.Reservation;

public class DoctorQueueEntryDto
{
    public int ReservationId { get; init; }
    public int PatientId { get; init; }
    public string PatientName { get; init; } = string.Empty;

    public int DoctorId { get; init; }
    public string DoctorName { get; init; } = string.Empty;

    public int ClinicId { get; init; }
    public string ClinicName { get; init; } = string.Empty;

    public string TreatmentTypeName { get; init; } = string.Empty;
    public TimeSpan ReservationTime { get; init; }

    public PatientQueueStatus QueueStatus { get; init; }
    public DateTime? CheckedInAtUtc { get; init; }
    public DateTime? ConsultationStartedAtUtc { get; init; }
}