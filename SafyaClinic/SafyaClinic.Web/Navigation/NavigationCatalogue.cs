using SafyaClinic.Application.Security;

namespace SafyaClinic.Web.Navigation;

/// <summary>A sidebar link as shown to the current user.</summary>
public sealed record NavLink(string Label, string Icon, string Controller, string Action, bool IsActive);

/// <summary>A labelled group of sidebar links (a workspace). Groups with no visible links are left out.</summary>
public sealed record NavSection(string? Label, IReadOnlyList<NavLink> Links);

/// <summary>
/// F7: the one list of sidebar destinations. Each link names the permission that shows it, so the
/// menu always matches what the user may open; the sidebar is rendered from this list.
/// </summary>
public static class NavigationCatalogue
{
    private sealed record Entry(
        string Label, string Icon, string Controller, string Action, string? Permission,
        string? AlternativeLabel = null, string? AlternativeLabelPermission = null,
        string[]? OnlyActions = null, string[]? ExceptActions = null);

    private static readonly (string? Label, Entry[] Entries)[] Sections =
    {
        (null, new[]
        {
            new Entry("Dashboard", "bi-speedometer2", "Dashboard", "Index", Permission: null),
        }),
        ("Front desk", new[]
        {
            new Entry("Patients", "bi-people", "Patients", "Index", Permissions.Patients.View),
            new Entry("Appointment Board", "bi-calendar3", "Reservations", "Board", Permissions.Reservations.View,
                OnlyActions: new[] { "Board" }),
            new Entry("Reservations", "bi-calendar2-week", "Reservations", "Index", Permissions.Reservations.View,
                ExceptActions: new[] { "Board", "Queue" }),
            new Entry("My Queue", "bi-person-lines-fill", "Reservations", "Queue", Permissions.Reservations.ViewQueue,
                AlternativeLabel: "Doctor Queues", AlternativeLabelPermission: Permissions.Reservations.ViewAllQueues,
                OnlyActions: new[] { "Queue" }),
            new Entry("Follow-ups", "bi-calendar-check", "FollowUps", "Index", Permissions.FollowUps.View),
        }),
        ("Clinical", new[]
        {
            new Entry("Analysis", "bi-clipboard2-pulse", "Analysis", "Index", Permissions.Analyses.View),
            new Entry("Nutrition", "bi-capsule", "Nutrition", "Packages", Permissions.Nutrition.View),
        }),
        ("Billing", new[]
        {
            new Entry("Unpaid Payments", "bi-cash-coin", "Payments", "Dashboard", Permissions.Payments.View,
                AlternativeLabel: "Payment Dashboard", AlternativeLabelPermission: Permissions.Payments.ViewFullDashboard,
                ExceptActions: new[] { "Report" }),
            new Entry("Payment Reports", "bi-file-earmark-bar-graph", "Payments", "Report", Permissions.Payments.ViewReport,
                OnlyActions: new[] { "Report" }),
        }),
        ("Administration", new[]
        {
            new Entry("Users", "bi-person-gear", "Users", "Index", Permissions.Users.Manage),
            new Entry("Permission Groups", "bi-shield-lock", "PermissionGroups", "Index", Permissions.Users.Manage),
            new Entry("Lookups", "bi-sliders", "Lookups", "Index", Permissions.Settings.Lookups),
            new Entry("Patient Sources", "bi-diagram-3", "PatientSources", "Index", Permissions.Settings.PatientSources),
            new Entry("Clinics", "bi-hospital", "Clinics", "Index", Permissions.Settings.Clinics),
        }),
    };

    /// <summary>Every permission the menu depends on (to show a link or choose its label).</summary>
    public static IReadOnlyList<string> RelevantPermissions { get; } = Sections
        .SelectMany(s => s.Entries)
        .SelectMany(e => new[] { e.Permission, e.AlternativeLabelPermission })
        .OfType<string>()
        .Distinct()
        .ToList();

    /// <summary>Every link destination, for tests: (controller, action).</summary>
    public static IReadOnlyList<(string Controller, string Action)> Destinations { get; } = Sections
        .SelectMany(s => s.Entries)
        .Select(e => (e.Controller, e.Action))
        .ToList();

    /// <summary>The sections and links a user with these permissions sees, with the current page marked.</summary>
    public static IReadOnlyList<NavSection> For(IReadOnlySet<string> permissions, string? controller, string? action)
    {
        var sections = new List<NavSection>();
        foreach (var (label, entries) in Sections)
        {
            var links = entries
                .Where(e => e.Permission is null || permissions.Contains(e.Permission))
                .Select(e => new NavLink(
                    e.AlternativeLabelPermission is not null && permissions.Contains(e.AlternativeLabelPermission)
                        ? e.AlternativeLabel!
                        : e.Label,
                    e.Icon, e.Controller, e.Action, IsActive(e, controller, action ?? string.Empty)))
                .ToList();
            if (links.Count > 0) sections.Add(new NavSection(label, links));
        }
        return sections;
    }

    private static bool IsActive(Entry entry, string? controller, string action)
    {
        if (!string.Equals(entry.Controller, controller, StringComparison.OrdinalIgnoreCase)) return false;
        if (entry.OnlyActions is not null) return entry.OnlyActions.Contains(action, StringComparer.OrdinalIgnoreCase);
        if (entry.ExceptActions is not null) return !entry.ExceptActions.Contains(action, StringComparer.OrdinalIgnoreCase);
        return true;
    }
}
