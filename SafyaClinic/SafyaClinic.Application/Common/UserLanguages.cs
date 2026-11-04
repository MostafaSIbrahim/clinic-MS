namespace SafyaClinic.Application.Common;

/// <summary>F9: the languages the app offers. English is the default (K5).</summary>
public static class UserLanguages
{
    public const string English = "en";
    public const string Arabic = "ar";

    public static IReadOnlyList<string> All { get; } = new[] { English, Arabic };

    /// <summary>"AR " → "ar"; anything not offered → null.</summary>
    public static string? Normalize(string? language)
    {
        var code = language?.Trim().ToLowerInvariant();
        return code is not null && All.Contains(code) ? code : null;
    }
}
