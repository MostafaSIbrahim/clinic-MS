namespace SafyaClinic.Application.DTOs.MedicalRecord;

public class PatientFollowUpFilter
{
    public int? DoctorId { get; init; }

    // All, Overdue, Today, Upcoming
    public string Due { get; init; } = "All";

    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

public class PatientFollowUpDto
{
    public int RecordId { get; init; }
    public int PatientId { get; init; }
    public string PatientName { get; init; } = string.Empty;

    public int DoctorId { get; init; }
    public string DoctorName { get; init; } = string.Empty;

    public DateTime FollowUpDate { get; init; }
    public int? OriginalReservationId { get; init; }
}