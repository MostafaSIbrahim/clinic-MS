namespace SafyaClinic.Application.Security;

/// <summary>Sign-in lockout (S5). Bound from configuration section "Security:Lockout".</summary>
public sealed class LockoutSettings
{
    public const string SectionName = "Security:Lockout";

    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}