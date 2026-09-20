namespace SafyaClinic.Application.DTOs.MedicalRecord;

public class FollowUpBookingContextDto
{
    public int RecordId { get; init; }
    public int PatientId { get; init; }
    public string PatientName { get; init; } = "";
    public int DoctorId { get; init; }
    public string DoctorName { get; init; } = "";
    public string Category { get; init; } = "";
    public DateTime FollowUpDate { get; init; }
}