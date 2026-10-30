namespace SafyaClinic.Application.Security;

/// <summary>Random value that changes whenever a user's sessions must end (S6).</summary>
public static class SecurityStamps
{
    public static string New() => Guid.NewGuid().ToString("N");
}