using System.Text.RegularExpressions;

namespace SafyaClinic.Application.Common;

/// <summary>
/// Readable labels for code values shown to staff (F2): "InternalMedicine" → "Internal Medicine",
/// "NoShow" → "No Show". Interim — F9 moves labels to resource files for English/Arabic.
/// </summary>
public static class Labels
{
    public static string Humanize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value.Trim(), "(?<=[a-z0-9])(?=[A-Z])", " ");
}