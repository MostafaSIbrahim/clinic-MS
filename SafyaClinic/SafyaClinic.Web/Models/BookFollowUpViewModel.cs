using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using SafyaClinic.Application.DTOs.MedicalRecord;

namespace SafyaClinic.Web.Models;

public class BookFollowUpViewModel
{
    [Range(1, int.MaxValue)]
    public int RecordId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a clinic.")]
    public int ClinicId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a treatment.")]
    public int TreatmentTypeId { get; set; }

    [Required]
    public DateTime? ReservationDate { get; set; }

    [Required]
    public TimeSpan? ReservationTime { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [BindNever]
    public FollowUpBookingContextDto? Context { get; set; }

    [BindNever]
    public List<SelectListItem> Clinics { get; set; } = new();

    [BindNever]
    public List<SelectListItem> Treatments { get; set; } = new();
}