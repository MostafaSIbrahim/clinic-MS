using Microsoft.AspNetCore.Mvc.Rendering;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.MedicalRecord;

namespace SafyaClinic.Web.Models;

public class PatientFollowUpsViewModel
{
    public int? DoctorId { get; set; }
    public string Due { get; set; } = "All";
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public bool CanViewAll { get; init; }

    public List<SelectListItem> Doctors { get; init; } = new();

    public PagedResult<PatientFollowUpDto> Results { get; init; }
        = new()
        {
            Page = 1,
            PageSize = 20
        };
}