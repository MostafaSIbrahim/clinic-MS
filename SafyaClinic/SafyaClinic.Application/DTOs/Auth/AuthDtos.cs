using SafyaClinic.Domain.Enums;

namespace SafyaClinic.Application.DTOs.Auth;

public class LoginRequest
{
    public string PhoneNumber { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public class LoginResponse
{
    public int UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public bool MustChangePassword { get; init; }
    public string SecurityStamp { get; init; } = string.Empty;
    public ClinicianType ClinicianType { get; init; }   // F4c: whose day the dashboard shows
    public string? PreferredLanguage { get; init; }     // F9c: the user's saved language, if any
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
}