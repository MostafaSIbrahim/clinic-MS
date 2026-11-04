using Microsoft.AspNetCore.Mvc;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Interfaces.Services;

namespace SafyaClinic.Web.Components;

/// <summary>F8: what the patient header shows. Allergies are never hidden or summarised.</summary>
public sealed record PatientHeaderModel(
    int Id, string Name, string? AgeAndSex, string? Phone, string? BloodType, string? Allergies, string? ChronicDiseases)
{
    public bool HasAllergies => !string.IsNullOrWhiteSpace(Allergies);

    public static PatientHeaderModel From(PatientDto patient)
    {
        var ageAndSex = string.Join(" · ", new[]
        {
            patient.Age is int age ? $"{age} y" : null,
            patient.Gender
        }.Where(part => !string.IsNullOrWhiteSpace(part)));

        var phone = (patient.Phones.FirstOrDefault(p => p.IsPrimary) ?? patient.Phones.FirstOrDefault())?.PhoneNumber;

        return new PatientHeaderModel(
            patient.Id,
            patient.FullName,
            ageAndSex.Length == 0 ? null : ageAndSex,
            phone,
            Clean(patient.BloodType),
            Clean(patient.Allergies),
            Clean(patient.ChronicDiseases));
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>
/// F8: the patient header shown at the top of clinical screens: who the patient is, how to reach them,
/// and their allergies and chronic diseases, so they are visible wherever decisions are made.
/// Usage: <c>@await Component.InvokeAsync("PatientHeader", new { patientId = Model.PatientId })</c>.
/// </summary>
public class PatientHeaderViewComponent : ViewComponent
{
    private readonly IPatientService _patients;

    public PatientHeaderViewComponent(IPatientService patients) => _patients = patients;

    public async Task<IViewComponentResult> InvokeAsync(int patientId)
    {
        var result = await _patients.GetPatientByIdAsync(patientId);
        if (!result.IsSuccess || result.Data is null)
            return Content(string.Empty);

        return View(PatientHeaderModel.From(result.Data));
    }
}
