using SafyaClinic.Application.DTOs.Analysis;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.MedicalRecord;
using SafyaClinic.Application.DTOs.Nutrition;
using SafyaClinic.Application.DTOs.Payment;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.DTOs.Reservation;
using SafyaClinic.Application.Security;

namespace SafyaClinic.Web.Models;

/// <summary>M1: a tab of the patient workspace, shown to people with its permission.</summary>
public sealed record PatientTab(string Key, string Label, string Permission);

/// <summary>
/// M1: the tabs of the patient workspace (<c>/Patients/Details/5?tab=visits</c>). Each tab is its own page load,
/// so a long history stays fast and every tab can be bookmarked. M1b–M1d add Records, Lab, Nutrition, Payments
/// and Timeline.
/// </summary>
public static class PatientTabs
{
    public const string Overview = "overview";
    public const string Visits = "visits";
    public const string Records = "records";   // M1b
    public const string Lab = "lab";           // M1b
    public const string Nutrition = "nutrition"; // M1c
    public const string Payments = "payments";   // M1c

    /// <summary>Tabs that show the patient header with allergies above them.</summary>
    public static bool IsClinical(string tab) => tab is Records or Lab;

    public static readonly IReadOnlyList<PatientTab> All = new[]
    {
        new PatientTab(Overview, "Overview", Permissions.Patients.View),
        new PatientTab(Visits, "Visits", Permissions.Reservations.View),
        new PatientTab(Records, "Records", Permissions.Records.View),
        new PatientTab(Lab, "Lab", Permissions.Analyses.View),
        new PatientTab(Nutrition, "Nutrition", Permissions.Nutrition.View),
        new PatientTab(Payments, "Payments", Permissions.Payments.View),
    };

    public static async Task<IReadOnlyList<PatientTab>> VisibleAsync(Func<string, Task<bool>> can)
    {
        var visible = new List<PatientTab>();
        foreach (var tab in All)
            if (await can(tab.Permission)) visible.Add(tab);
        return visible;
    }

    /// <summary>
    /// The requested tab if the person can see it. Otherwise the tab that fits their work: Visits for the
    /// front desk (people who check patients in), Records for doctors (people who read records), Nutrition for
    /// nutritionists, Payments for people who only handle money, else Overview.
    /// </summary>
    public static async Task<string> ChooseAsync(string? requested, IReadOnlyList<PatientTab> visible, Func<string, Task<bool>> can)
    {
        var key = requested?.Trim().ToLowerInvariant();
        if (key is not null && visible.Any(t => t.Key == key)) return key;
        if (visible.Any(t => t.Key == Visits) && await can(Permissions.Reservations.CheckIn)) return Visits;
        if (visible.Any(t => t.Key == Records)) return Records;
        if (visible.Any(t => t.Key == Nutrition)) return Nutrition;
        if (visible.Any(t => t.Key == Payments)) return Payments;
        return Overview;
    }
}

/// <summary>M1: what the patient workspace page shows.</summary>
public sealed class PatientWorkspaceModel
{
    public required PatientDashboardDto Dashboard { get; init; }
    public required IReadOnlyList<PatientTab> Tabs { get; init; }
    public required string ActiveTab { get; init; }

    /// <summary>Visits tab: one page of the patient's reservations, newest first.</summary>
    public PagedResult<ReservationSummaryDto>? Visits { get; init; }

    /// <summary>Records tab (M1b): the patient's medical records, newest first.</summary>
    public IReadOnlyList<PatientRecordDto>? Records { get; init; }

    /// <summary>Lab tab (M1b): the patient's lab requests.</summary>
    public IReadOnlyList<MedicalAnalysisDto>? Lab { get; init; }

    /// <summary>Nutrition tab (M1c): the patient's enrollments.</summary>
    public IReadOnlyList<PatientEnrollmentDto>? Enrollments { get; init; }

    /// <summary>Payments tab (M1c): balances, collection and payment history.</summary>
    public PatientFinancialSummaryDto? Payments { get; init; }
}
