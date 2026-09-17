namespace SafyaClinic.Application.DTOs.Patient;

public enum PatientTimelineEventType
{
    Reservation = 1,
    MedicalRecord = 2
}

public class PatientTimelineItemDto
{
    public PatientTimelineEventType EventType { get; init; }

    public int EntityId { get; init; }

    public DateTime OccurredAt { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;
}