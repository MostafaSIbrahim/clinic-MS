using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SafyaClinic.Application.DTOs.Auth;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.Security;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Domain.Interfaces.Repositories;
using SafyaClinic.Application.Common.Auditing;

namespace SafyaClinic.Application.Services;

public class AuthService : IAuthService
{
    private const string InvalidCredentials = "Invalid phone number or password.";

    private readonly IUnitOfWork _uow;
    private readonly ILogger<AuthService> _logger;
    private readonly LockoutSettings _lockout;
    private readonly IAuditTrail _audit;

    public AuthService(IUnitOfWork uow, ILogger<AuthService> logger, LockoutSettings lockout, IAuditTrail? audit = null)
    {
        _uow = uow;
        _logger = logger;
        _lockout = lockout;
        _audit = audit ?? NoAuditTrail.Instance;
    }

    public async Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request)
    {
        var user = await _uow.Users.FirstOrDefaultAsync(
            u => u.PhoneNumber == request.PhoneNumber && u.IsActive);

        if (user is null)
        {
            _logger.LogWarning("Sign-in failed: no active account for the given phone number.");
            await _audit.RecordAsync(AuditActions.SignInFailed, "User", null, new { reason = "no active account" });
            await _uow.SaveChangesAsync();
            return ServiceResult<LoginResponse>.Failure(InvalidCredentials);
        }

        var now = DateTime.UtcNow;
        if (user.LockoutEndUtc is { } lockedUntil && lockedUntil > now)
        {
            _logger.LogWarning("Sign-in refused for user {UserId}: locked until {LockoutEndUtc:u}.", user.Id, lockedUntil);
            await _audit.RecordAsync(AuditActions.SignInRefusedWhileLocked, "User", user.Id, new { lockedUntilUtc = lockedUntil });
            await _uow.SaveChangesAsync();
            var minutes = (int)Math.Ceiling((lockedUntil - now).TotalMinutes);
            return ServiceResult<LoginResponse>.Failure(
                $"This account is locked after too many failed sign-in attempts. Try again in {minutes} minute(s).");
        }

        var check = PasswordHashing.Verify(request.Password, user.PasswordHash);
        if (check == PasswordCheck.Failed)
        {
            await RecordFailedAttemptAsync(user, now);
            return ServiceResult<LoginResponse>.Failure(InvalidCredentials);
        }

        // Old-format or weaker hash: store a current one now that we know the password.
        if (check == PasswordCheck.SuccessRehashNeeded)
            user.PasswordHash = PasswordHashing.Hash(request.Password);

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAt = now;
        await _audit.RecordAsync(AuditActions.SignInSucceeded, "User", user.Id, actingUserId: user.Id);
        if (string.IsNullOrEmpty(user.SecurityStamp))
            user.SecurityStamp = SecurityStamps.New();   // users created before S6
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();

        var roleNames = await _uow.UserRoles
            .Query()
            .Where(ur => ur.UserId == user.Id)
            .OrderBy(ur => ur.RoleId)
            .Select(ur => ur.Role.RoleName)
            .ToListAsync();

        return ServiceResult<LoginResponse>.Success(new LoginResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            ExpiresAt = now.AddHours(8),
            Roles = roleNames,
            SecurityStamp = user.SecurityStamp,
            MustChangePassword = user.MustChangePassword
        });
    }

    public async Task<ServiceResult> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null)
            return ServiceResult.Failure("User not found.");

        if (PasswordHashing.Verify(request.CurrentPassword, user.PasswordHash) == PasswordCheck.Failed)
            return ServiceResult.Failure("Current password is incorrect.");

        var problem = PasswordHashing.Validate(request.NewPassword, request.ConfirmPassword);
        if (problem is not null)
            return ServiceResult.Failure(problem);

        if (request.NewPassword == request.CurrentPassword)
            return ServiceResult.Failure("Choose a password that is different from the current one.");

        user.PasswordHash = PasswordHashing.Hash(request.NewPassword);
        user.MustChangePassword = false;
        user.SecurityStamp = SecurityStamps.New();   // ends every session, including this one
        await _audit.RecordAsync(AuditActions.PasswordChanged, "User", user.Id, actingUserId: user.Id);
        
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
        _logger.LogInformation("User {UserId} changed their password.", user.Id);
        return ServiceResult.Success("Password changed successfully.");
    
    }



    public async Task<ServiceResult<UserDto>> GetCurrentUserAsync(int userId)
    {
        var user = await _uow.Users
            .Query()
            .Where(u => u.Id == userId)
            .Select(u => new UserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Specialization = u.Specialization,
                IsActive = u.IsActive,
                LastLoginAt = u.LastLoginAt,
                Roles = u.UserRoles
                    .OrderBy(ur => ur.RoleId)
                    .Select(ur => ur.Role.RoleName)
            })
            .FirstOrDefaultAsync();

        if (user is null)
            return ServiceResult<UserDto>.Failure("User not found.");

        return ServiceResult<UserDto>.Success(user);
    }

    public Task<string?> GetActiveSecurityStampAsync(int userId) =>
        _uow.Users
            .Query()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => (string?)u.SecurityStamp)
            .FirstOrDefaultAsync();
    private async Task RecordFailedAttemptAsync(User user, DateTime now)
    {
        user.FailedLoginCount++;
        if (user.FailedLoginCount >= _lockout.MaxFailedAttempts)
        {
            user.FailedLoginCount = 0;
            user.LockoutEndUtc = now.AddMinutes(_lockout.LockoutMinutes);
            _logger.LogWarning("Sign-in failed for user {UserId}; account locked until {LockoutEndUtc:u}.",
                user.Id, user.LockoutEndUtc);
        }
        else
        {
            _logger.LogWarning("Sign-in failed for user {UserId} (attempt {Attempt} of {MaxAttempts}).",
                user.Id, user.FailedLoginCount, _lockout.MaxFailedAttempts);
        }

        await _audit.RecordAsync(
        user.LockoutEndUtc is not null && user.FailedLoginCount == 0 ? AuditActions.SignInLockedOut : AuditActions.SignInFailed,
        "User", user.Id,
        new { attempt = user.FailedLoginCount, lockedUntilUtc = user.LockoutEndUtc });

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
    }
}
    