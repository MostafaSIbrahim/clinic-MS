namespace SafyaClinic.Application.Services;

/// <summary>
/// Interim reservation rules until permission groups ship (S7; decisions D4 and D6).
/// Roles are read from the database by the caller, never from the browser.
/// </summary>
public static class ReservationRules
{
    public const string AdminRole = "Admin";
    public const string DoctorRole = "Doctor";
    public const string NutritionistRole = "Nutritionist";
    public const string CompletedStatus = "Completed";

    /// <summary>D4: only an Admin may set a price different from the treatment type's.</summary>
    public static bool CanOverridePrice(IReadOnlyCollection<string> roles) => roles.Contains(AdminRole);

    /// <summary>Returns a message for the user, or null when the status change is allowed.</summary>
    public static string? CheckStatusChange(
        IReadOnlyCollection<string> roles, int actingUserId, int reservationDoctorId, string targetStatus)
    {
        var isAdmin = roles.Contains(AdminRole);

        // D4: doctors complete a visit from the consultation screen, which records it properly.
        if (targetStatus == CompletedStatus && !isAdmin)
            return "Only an administrator can set a visit to Completed here. The doctor completes it from the consultation screen.";

        // D6: a clinician never changes the status of another doctor's reservation.
        var isClinician = roles.Contains(DoctorRole) || roles.Contains(NutritionistRole);
        if (isClinician && !isAdmin && reservationDoctorId != actingUserId)
            return "You cannot change the status of another doctor's reservation.";

        return null;
    }
}