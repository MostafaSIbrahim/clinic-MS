using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SafyaClinic.Web.Models;

/// <summary>F8c: sample data for the component sample page. Nothing here is saved.</summary>
public class ComponentSampleModel
{
    [Required]
    [Display(Name = "Full name")]
    public string? FullName { get; set; } = "Mona Adel";

    [EmailAddress]
    public string? Email { get; set; }

    [Phone]
    [Display(Name = "Mobile phone")]
    public string? Phone { get; set; } = "0100 123 4567";

    [Display(Name = "Date of birth")]
    public DateTime? BirthDate { get; set; } = new DateTime(1990, 5, 14);

    [Required]
    public int? ClinicId { get; set; }

    public string? Notes { get; set; }

    public IEnumerable<SelectListItem> Clinics { get; } = new[]
    {
        new SelectListItem("Main clinic", "1"),
        new SelectListItem("Nasr City branch", "2"),
    };

    public IReadOnlyList<SampleRow> Rows { get; } = new[]
    {
        new SampleRow("Mona Adel", "0100 123 4567", "Waiting", 450m, new DateTime(2026, 11, 4, 8, 30, 0, DateTimeKind.Utc)),
        new SampleRow("Karim Fathy", "0111 555 2020", "InConsultation", 600m, new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc)),
        new SampleRow("سارة محمود", "0122 987 6543", "Completed", 0m, new DateTime(2026, 11, 4, 9, 45, 0, DateTimeKind.Utc)),
    };

    public static IReadOnlyList<string> Statuses { get; } = new[]
    {
        "Pending", "Waiting", "InConsultation", "Completed", "Cancelled", "NoShow", "Paid", "Due", "WriteOff", "Unknown",
    };
}

public sealed record SampleRow(string Name, string Phone, string Status, decimal Due, DateTime ArrivedUtc);
