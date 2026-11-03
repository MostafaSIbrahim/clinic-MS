using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Common.Auditing;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Application.Security;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Domain.Interfaces.Repositories;
using SafyaClinic.Domain.Enums;

namespace SafyaClinic.Application.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditTrail _audit;

    public UserService(IUnitOfWork uow, IAuditTrail? audit = null)
    {
        _uow = uow;
        _audit = audit ?? NoAuditTrail.Instance;
    }

    public async Task<ServiceResult<UserDto>> CreateUserAsync(
        CreateUserRequest request, int createdByUserId)
    {
        var passwordProblem = PasswordHashing.Validate(request.Password, request.ConfirmPassword);
        if (passwordProblem is not null)
            return ServiceResult<UserDto>.Failure(passwordProblem);
        // Phone uniqueness check
        if (await _uow.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber) is not null)
            return ServiceResult<UserDto>.Failure("A user with this phone number already exists.");

        // F4d: groups are added by the caller after creation; the clinician type is chosen directly.
        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            PasswordHash = PasswordHashing.Hash(request.Password),
            SecurityStamp = SecurityStamps.New(),
            Specialization = request.Specialization,
            LicenseNumber = request.LicenseNumber,
            ClinicianType = request.ClinicianType,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdByUserId
        };

        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync();
        await _audit.RecordAsync(AuditActions.UserCreated, "User", user.Id,
            new { clinicianType = request.ClinicianType.ToString() }, createdByUserId);
        await _uow.SaveChangesAsync();

        return ServiceResult<UserDto>.Success(await MapUserDtoAsync(user));
    }


    public async Task<ServiceResult> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var problem = PasswordHashing.Validate(request.NewPassword, request.ConfirmPassword);
        if (problem is not null)
            return ServiceResult.Failure(problem);

        var user = await _uow.Users.GetByIdAsync(request.UserId);
        if (user is null)
            return ServiceResult.Failure("User not found.");

        user.PasswordHash = PasswordHashing.Hash(request.NewPassword);
        user.MustChangePassword = true;   // the user picks their own password at next sign-in
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;        // a reset also unlocks the account
        user.SecurityStamp = SecurityStamps.New();   // ends the user's open sessions
        await _audit.RecordAsync(AuditActions.PasswordReset, "User", user.Id);
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();

        return ServiceResult.Success("Password reset. The user must choose a new password at next sign-in.");
    }
    public async Task<ServiceResult<UserDto>> GetUserByIdAsync(int userId)
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
                ClinicianType = u.ClinicianType,
                Groups = u.UserGroups
                    .OrderBy(m => m.Group.Name)
                    .Select(m => m.Group.Name)
            })
            .FirstOrDefaultAsync();

        if (user is null)
            return ServiceResult<UserDto>.Failure("User not found.");

        return ServiceResult<UserDto>.Success(user);
    }

    public async Task<ServiceResult<PagedResult<UserDto>>> GetAllUsersAsync(
         PaginationRequest request)
    {
        var query = _uow.Users.Query();

        var totalCount = await query.CountAsync();

        var users = await query
            .OrderBy(u => u.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Specialization = u.Specialization,
                IsActive = u.IsActive,
                LastLoginAt = u.LastLoginAt,
                ClinicianType = u.ClinicianType,
                Groups = u.UserGroups
                    .OrderBy(m => m.Group.Name)
                    .Select(m => m.Group.Name)
            })
            .ToListAsync();

        return ServiceResult<PagedResult<UserDto>>.Success(
            new PagedResult<UserDto>
            {
                Items = users,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            });
    }

    public async Task<ServiceResult<IEnumerable<UserDto>>> GetDoctorsAsync()
    {
        //F4a: clinicians are marked by ClinicianType (was role IDs 2 and 5).
        var doctors = await _uow.Users
            .Query()
            .Where(u => u.IsActive && u.ClinicianType != ClinicianType.None)
            .Select(u => new UserDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Specialization = u.Specialization,
                IsActive = u.IsActive,
                LastLoginAt = u.LastLoginAt,
                ClinicianType = u.ClinicianType,
                Groups = u.UserGroups
                    .OrderBy(m => m.Group.Name)
                    .Select(m => m.Group.Name)
            })
            .ToListAsync();

        return ServiceResult<IEnumerable<UserDto>>.Success(doctors);
    }

    private const string LastAdminMessage =
        "This is the last active administrator. Add another active user to the Administrators group first.";

    public async Task<ServiceResult> SetUserActiveAsync(int userId, bool isActive, int actingUserId)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null)
            return ServiceResult.Failure("User not found.");

        if (!isActive)
        {
            if (userId == actingUserId)
                return ServiceResult.Failure("You cannot deactivate your own account.");
            if (await IsLastActiveAdminAsync(userId))
                return ServiceResult.Failure(LastAdminMessage);
        }

        if (user.IsActive == isActive)
            return ServiceResult.Success(isActive ? "User is already active." : "User is already inactive.");

        user.IsActive = isActive;
        user.SecurityStamp = SecurityStamps.New();   // a deactivated user's open sessions end
        await _audit.RecordAsync(isActive ? AuditActions.UserActivated : AuditActions.UserDeactivated,
            "User", user.Id, actingUserId: actingUserId);
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();

        return ServiceResult.Success(isActive ? "User activated." : "User deactivated and signed out.");
    }

    /// <summary>F4d: who appears in doctor pickers and queues. The user signs in again (the type is in the cookie).</summary>
    public async Task<ServiceResult> SetClinicianTypeAsync(int userId, ClinicianType clinicianType, int actingUserId)
    {
        if (!Enum.IsDefined(clinicianType))
            return ServiceResult.Failure("Choose a valid clinician type.");

        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null)
            return ServiceResult.Failure("User not found.");
        if (user.ClinicianType == clinicianType)
            return ServiceResult.Success("Clinician type unchanged.");

        var previous = user.ClinicianType;
        user.ClinicianType = clinicianType;
        user.SecurityStamp = SecurityStamps.New();
        await _audit.RecordAsync(AuditActions.ClinicianTypeChanged, "User", userId,
            new { from = previous.ToString(), to = clinicianType.ToString() }, actingUserId);
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Clinician type changed. The user will be asked to sign in again.");
    }

    // F4d: administrators are the active members of the Administrators group.
    private async Task<bool> IsLastActiveAdminAsync(int userId)
    {
        var activeAdminIds = await _uow.Users
            .Query()
            .Where(u => u.IsActive && u.UserGroups.Any(m => m.Group.SystemKey == SystemGroups.AdminRole))
            .Select(u => u.Id)
            .ToListAsync();

        return activeAdminIds.Count == 1 && activeAdminIds[0] == userId;
    }

    // ── Mapper ────────────────────────────────────────────────

    private async Task<UserDto> MapUserDtoAsync(User user)
    {
        var dto = await _uow.Users
                .Query()
                .Where(u => u.Id == user.Id)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    Specialization = u.Specialization,
                    IsActive = u.IsActive,
                    LastLoginAt = u.LastLoginAt,
                    ClinicianType = u.ClinicianType,
                    Groups = u.UserGroups
                        .OrderBy(m => m.Group.Name)
                        .Select(m => m.Group.Name)
                })
                .FirstOrDefaultAsync();

        if (dto is not null)
            return dto;

        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Specialization = user.Specialization,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt,
            ClinicianType = user.ClinicianType,
            Groups = Enumerable.Empty<string>()
        };
    }
}