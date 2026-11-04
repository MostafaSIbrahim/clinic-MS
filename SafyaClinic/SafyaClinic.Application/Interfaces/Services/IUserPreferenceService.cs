using SafyaClinic.Application.DTOs.Common;

namespace SafyaClinic.Application.Interfaces.Services;

/// <summary>F9c: a user's own settings, which follow them to any browser.</summary>
public interface IUserPreferenceService
{
    /// <summary>Saves the user's language ("en" or "ar").</summary>
    Task<ServiceResult> SetLanguageAsync(int userId, string? language);
}
