namespace SafyaClinic.Web.Security;

/// <summary>
/// The role-based policies in use before permissions (moved unchanged from Program.cs).
/// F4b-2 replaces every use with [RequirePermission] and deletes this class.
/// </summary>
public static class LegacyRolePolicies
{
    public static readonly IReadOnlyDictionary<string, string[]> All = new Dictionary<string, string[]>
    {
        ["PaymentStaff"] = new[] { "Admin", "Reception", "ReceptionAdmin" },
        ["PaymentReports"] = new[] { "Admin", "Reception", "ReceptionAdmin", "Doctor" },
        ["AdminOnly"] = new[] { "Admin" },
        ["DoctorOrAdmin"] = new[] { "Admin", "Doctor" },
        ["ReceptionOrAdmin"] = new[] { "Admin", "Reception" },
        ["NutritionTeam"] = new[] { "Admin", "Nutritionist" },
        ["ClinicalStaff"] = new[] { "Admin", "Doctor", "Nutritionist", "Reception" },
    };
}