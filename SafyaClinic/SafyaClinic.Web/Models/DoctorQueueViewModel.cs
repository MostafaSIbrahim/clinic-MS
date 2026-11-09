using Microsoft.AspNetCore.Mvc.Rendering;
using SafyaClinic.Application.DTOs.Reservation;

namespace SafyaClinic.Web.Models;

public class DoctorQueueViewModel
{
    public int? ClinicId { get; set; }
    public int? DoctorId { get; set; }
    public bool CanViewAllQueues { get; init; }

    public List<SelectListItem> Clinics { get; init; } = new();
    public List<SelectListItem> Doctors { get; init; } = new();

    public List<DoctorQueueEntryDto> Entries { get; init; } = new();

    /// <summary>Booked for today but not checked in yet (shown under the queue).</summary>
    public List<DoctorQueueEntryDto> NotArrived { get; init; } = new();
}