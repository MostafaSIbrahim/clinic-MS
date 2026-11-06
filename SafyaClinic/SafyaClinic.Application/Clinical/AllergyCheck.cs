using System.Text.RegularExpressions;

namespace SafyaClinic.Application.Clinical;

/// <summary>A medicine whose name matches one of the patient's recorded allergies.</summary>
public sealed record AllergyMatch(string Medication, string Allergy);

/// <summary>
/// P5a: compares medicine names with the patient's recorded allergies (free text, e.g. "Penicillin, sulfa drugs").
/// <para>
/// The allergy text is split on commas, semicolons, the Arabic comma, slashes, new lines and the word "and".
/// Terms shorter than 3 letters and "none"-style entries are ignored. A medicine matches when its name contains an
/// allergy term, or when an allergy term contains the medicine's first word (4 letters or more). Case doesn't matter.
/// </para>
/// <para>
/// This is a <b>name</b> check only: it does not know drug classes (an allergy to penicillin does not flag
/// amoxicillin). It supports the doctor's judgement; it doesn't replace it. The same rules run in the browser
/// (wwwroot/js/allergy-check.js) so the warning shows while typing; the server's check is the one that counts.
/// </para>
/// </summary>
public static class AllergyCheck
{
    private static readonly Regex Separators = new(@"[,;،/\r\n]+|\s+and\s+|\s+و\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> NoAllergy = new(StringComparer.OrdinalIgnoreCase)
    {
        "none", "no", "nil", "n/a", "na", "nka", "nkda", "no known allergies", "no known allergy", "لا يوجد", "لا توجد",
    };

    public static IReadOnlyList<string> Terms(string? allergies) =>
        string.IsNullOrWhiteSpace(allergies)
            ? Array.Empty<string>()
            : Separators.Split(allergies)
                .Select(t => t.Trim().ToLowerInvariant())
                .Where(t => t.Length >= 3 && !NoAllergy.Contains(t))
                .Distinct()
                .ToList();

    public static IReadOnlyList<AllergyMatch> Matches(string? allergies, IEnumerable<string?> medications)
    {
        var terms = Terms(allergies);
        if (terms.Count == 0) return Array.Empty<AllergyMatch>();

        var matches = new List<AllergyMatch>();
        foreach (var medication in medications.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m!.Trim()))
        {
            var name = medication.ToLowerInvariant();
            var firstWord = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            var term = terms.FirstOrDefault(t => name.Contains(t) || (firstWord.Length >= 4 && t.Contains(firstWord)));
            if (term is not null) matches.Add(new AllergyMatch(medication, term));
        }
        return matches;
    }

    private const string WarningPrefix = "Allergy warning: ";

    /// <summary>True for the service's refusal message, so a screen can show its own (translated) warning instead.</summary>
    public static bool IsWarning(string? message) => message?.StartsWith(WarningPrefix, StringComparison.Ordinal) == true;

    /// <summary>"Allergy warning: Penicillin V 500mg matches the recorded allergy "penicillin". …"</summary>
    public static string Warning(IEnumerable<AllergyMatch> matches) =>
        WarningPrefix + string.Join("; ", matches.Select(m => $"{m.Medication} matches the recorded allergy \"{m.Allergy}\""))
        + ". Check with the patient, then tick \"Prescribe anyway\" to add it.";
}
