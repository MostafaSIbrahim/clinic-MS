using SafyaClinic.Domain.Enums;

namespace SafyaClinic.Application.Services;

/// <summary>
/// M4a: the nutrition enrollment status rules.
/// <list type="bullet">
/// <item>Active → On hold or Cancelled, and On hold → Active (resume) or Cancelled, by people who manage nutrition.</item>
/// <item>Completed is set automatically when the last week's follow-up is recorded, never by hand.</item>
/// <item>Completed and Cancelled are final: to continue, the patient is enrolled again.</item>
/// </list>
/// </summary>
public static class EnrollmentRules
{
    public const string FinalMessage =
        "This enrollment is completed or cancelled and can't be changed. To continue, enroll the patient again.";

    public const string CompletedByHandMessage =
        "An enrollment is completed automatically when its last week's follow-up is recorded.";

    public const string NotAllowedMessage = "This status change isn't allowed.";

    public const string AnotherActiveMessage =
        "The patient already has another active enrollment. Complete or cancel it first.";

    public static bool IsFinal(EnrollmentStatus status) =>
        status is EnrollmentStatus.Completed or EnrollmentStatus.Cancelled;

    /// <summary>The statuses a person may choose from <paramref name="current"/>.</summary>
    public static IReadOnlyList<EnrollmentStatus> NextStatuses(EnrollmentStatus current) => current switch
    {
        EnrollmentStatus.Active => new[] { EnrollmentStatus.OnHold, EnrollmentStatus.Cancelled },
        EnrollmentStatus.OnHold => new[] { EnrollmentStatus.Active, EnrollmentStatus.Cancelled },
        _ => Array.Empty<EnrollmentStatus>(),
    };

    /// <summary>Returns a message for the user, or null when a person may make this change.</summary>
    public static string? CheckStatusChange(EnrollmentStatus current, EnrollmentStatus target)
    {
        if (current == target) return null;
        if (IsFinal(current)) return FinalMessage;
        if (target == EnrollmentStatus.Completed) return CompletedByHandMessage;
        return NextStatuses(current).Contains(target) ? null : NotAllowedMessage;
    }
}
