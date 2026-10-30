using SafyaClinic.Application.DTOs.Auth;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Patient;

namespace SafyaClinic.Application.Interfaces.Services;

public interface IAuthService
{
    Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request);
    Task<ServiceResult> ChangePasswordAsync(int userId, ChangePasswordRequest request);
    Task<ServiceResult<UserDto>> GetCurrentUserAsync(int userId);
    /// <summary>The user's current security stamp, or null if the user is missing or inactive (S6).</summary>
    Task<string?> GetActiveSecurityStampAsync(int userId);
}