using SafyaClinic.Application.Security;

namespace SafyaClinic.Application.Services;

/// <summary>
/// Reservation rules (S7; decisions D4 and D6), decided by the acting user's permissions (F4b).
/// Permissions are read from the database by the caller, never from the browser.
/// </summary>
public static class ReservationRules
{
    public const string CompletedStatus = "Completed";

    /// <summary>D4: a price different from the treatment type's needs Reservations.OverridePrice.</summary>
    public static bool CanOverridePrice(IReadOnlySet<string> permissions) =>
        permissions.Contains(Permissions.Reservations.OverridePrice);

    /// <summary>Returns a message for the user, or null when the status change is allowed.</summary>
    public static string? CheckStatusChange(
        IReadOnlySet<string> permissions, int actingUserId, int reservationDoctorId, string targetStatus)
    {
        // D4: doctors complete a visit from the consultation screen, which records it properly.
        if (targetStatus == CompletedStatus && !permissions.Contains(Permissions.Reservations.SetCompleted))
            return "Only an administrator can set a visit to Completed here. The doctor completes it from the consultation screen.";

        // D6: without Reservations.ChangeAnyDoctorStatus, only one's own reservations.
        if (reservationDoctorId != actingUserId && !permissions.Contains(Permissions.Reservations.ChangeAnyDoctorStatus))
            return "You cannot change the status of another doctor's reservation.";

        return null;
    }
}