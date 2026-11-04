namespace SafyaClinic.Web.Components;

/// <summary>
/// F8: maps a status word to its status-token style (tokens.css "Domain status tokens").
/// Status is always shown as text; the colour only supports it.
/// </summary>
public static class StatusStyles
{
    private static readonly Dictionary<string, string> Styles = new(StringComparer.OrdinalIgnoreCase)
    {
        // reservations
        ["Pending"] = "booked",
        ["Confirmed"] = "booked",
        ["Completed"] = "completed",
        ["Cancelled"] = "cancelled",
        ["NoShow"] = "no-show",
        // queue
        ["NotCheckedIn"] = "booked",
        ["Waiting"] = "waiting",
        ["InConsultation"] = "in-consultation",
        ["Finished"] = "completed",
        ["Left"] = "no-show",
        // payments
        ["Covered"] = "paid",
        ["Paid"] = "paid",
        ["Due"] = "due",
        ["Not fully covered"] = "due",
        ["WriteOff"] = "write-off",
    };

    /// <summary>The CSS modifier for a status, for example "waiting"; "neutral" when unknown.</summary>
    public static string For(string? status) =>
        status is not null && Styles.TryGetValue(status.Trim(), out var style) ? style : "neutral";
}
