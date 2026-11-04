using SafyaClinic.Application.Common;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Domain.Interfaces.Repositories;

namespace SafyaClinic.Application.Services;

/// <summary>
/// F9c: saves a user's own settings. Changing the language is not a security event, so it is not
/// audited and does not end the user's other sessions.
/// </summary>
public class UserPreferenceService : IUserPreferenceService
{
    private readonly IUnitOfWork _uow;

    public UserPreferenceService(IUnitOfWork uow) => _uow = uow;

    public async Task<ServiceResult> SetLanguageAsync(int userId, string? language)
    {
        var code = UserLanguages.Normalize(language);
        if (code is null)
            return ServiceResult.Failure("Choose English or Arabic.");

        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null)
            return ServiceResult.Failure("User not found.");
        if (user.PreferredLanguage == code)
            return ServiceResult.Success();

        user.PreferredLanguage = code;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success();
    }
}
