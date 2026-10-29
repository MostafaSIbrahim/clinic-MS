namespace SafyaClinic.Web.Security;

public static class AuthClaims
{
    /// <summary>Present (value "true") while the user must choose a new password (S5).</summary>
    public const string MustChangePassword = "safya:must_change_password";
}