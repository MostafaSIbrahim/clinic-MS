namespace SafyaClinic.Domain.Enums;

public enum PatientQueueStatus
{
    NotCheckedIn = 0,
    Waiting = 1,
    InConsultation = 2,
    Finished = 3,
    Left = 4
}