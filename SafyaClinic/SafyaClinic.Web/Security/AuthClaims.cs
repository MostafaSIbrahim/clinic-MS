namespace SafyaClinic.Web.Security;

public static class AuthClaims
{
    /// <summary>Present (value "true") while the user must choose a new password (S5).</summary>
    public const string MustChangePassword = "safya:must_change_password";

    /// <summary>The user's security stamp at sign-in; a different stamp in the database ends the session (S6).</summary>
    public const string SecurityStamp = "safya:security_stamp";
}