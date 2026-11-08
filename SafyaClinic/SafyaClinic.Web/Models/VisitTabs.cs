namespace SafyaClinic.Web.Models;

/// <summary>
/// P5b: the tabs of the consultation workspace (the medical record page). Actions that change one part of the
/// visit come back to its tab, e.g. <c>/MedicalRecords/Details/5?tab=treatments</c>.
/// </summary>
public static class VisitTabs
{
    public const string Notes = "notes";
    public const string Treatments = "treatments";
    public const string Prescriptions = "prescriptions";
    public const string Lab = "lab";

    public static readonly IReadOnlyList<string> All = new[] { Notes, Treatments, Prescriptions, Lab };

    /// <summary>A known tab (any case), otherwise Notes.</summary>
    public static string Normalize(string? tab)
    {
        var key = tab?.Trim().ToLowerInvariant();
        return key is not null && All.Contains(key) ? key : Notes;
    }
}
