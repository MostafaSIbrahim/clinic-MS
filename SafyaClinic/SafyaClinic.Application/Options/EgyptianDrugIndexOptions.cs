namespace SafyaClinic.Application.Options;

// Bound from the "EgyptianDrugIndex" section of appsettings.json.
// Lets an admin point the app at a real drug-index API once one is
// available/licensed, without touching any code — or keep using the
// bundled local dataset in the meantime.
public class EgyptianDrugIndexOptions
{
    public const string SectionName = "EgyptianDrugIndex";

    // "Local"  -> serve suggestions from the bundled offline dataset (default,
    //             works out of the box, no external dependency or API key).
    // "Http"   -> call a real external Egyptian drug-index REST API.
    public string Provider { get; set; } = "Local";

    // ── Used only when Provider == "Http" ──────────────────────────
    public string BaseUrl { get; set; } = string.Empty;
    public string SearchPath { get; set; } = "api/v1/medicines";
    public string QueryParameterName { get; set; } = "search";
    public string? ApiKey { get; set; }
    public string ApiKeyHeaderName { get; set; } = "X-API-Key";
    public int TimeoutSeconds { get; set; } = 5;

    // ── Shared behavior ─────────────────────────────────────────────
    public int MinQueryLength { get; set; } = 2;
    public int MaxResults { get; set; } = 15;
    public int CacheMinutes { get; set; } = 60;
}
