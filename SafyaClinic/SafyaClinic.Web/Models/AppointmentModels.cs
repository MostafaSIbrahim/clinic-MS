using SafyaClinic.Application.DTOs.MedicalRecord;
using SafyaClinic.Application.DTOs.Settings;
using SafyaClinic.Application.Scheduling;

namespace SafyaClinic.Web.Models;

/// <summary>P2b: the appointments screen.</summary>
public class AppointmentsPageModel
{
    public AppointmentDayDto? Day { get; init; }
    public IReadOnlyList<ClinicDto> Clinics { get; init; } = Array.Empty<ClinicDto>();
    public IReadOnlyList<TreatmentTypeDto> TreatmentTypes { get; init; } = Array.Empty<TreatmentTypeDto>();
    public DateTime Date { get; init; }
    public int? ClinicId { get; init; }
    public int? TreatmentTypeId { get; init; }
    public bool CanBook { get; init; }
}

/// <summary>P2b: booking a free time: doctor, clinic, date and time come from the clicked time.</summary>
public class BookAppointmentForm
{
    public int DoctorId { get; set; }
    public int ClinicId { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan Time { get; set; }
    public int TreatmentTypeId { get; set; }
    public int PatientId { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }

    // Shown only.
    public string DoctorName { get; set; } = string.Empty;
    public string ClinicName { get; set; } = string.Empty;
    public string? PatientName { get; set; }
}
