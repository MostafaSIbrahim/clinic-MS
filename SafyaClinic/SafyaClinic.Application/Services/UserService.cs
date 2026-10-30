using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Domain.Interfaces.Repositories;
using SafyaClinic.Application.Security;

namespace SafyaClinic.Application.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;

    public UserService(IUnitOfWork uow) => _uow = uow;

    public async Task<ServiceResult<UserDto>> CreateUserAsync(
        CreateUserRequest request, int createdByUserId)
    {
        var passwordProblem = PasswordHashing.Validate(request.Password, request.ConfirmPassword);
        if (passwordProblem is not null)
            return ServiceResult<UserDto>.Failure(passwordProblem);
        // Phone uniqueness check
        if (await _uow.Users.FirstOrDefaultAsync(u => u.PhoneNumber == request.PhoneNumber) is not null)
            return ServiceResult<UserDto>.Failure("A user with this phone number already exists.");

        // Validate roles exist
        var requestedRoleIds = request.RoleIds.ToList();

        var existingRoleIds = (await _uow.Roles
            .Query()
            .Where(role => requestedRoleIds.Contains(role.Id))
            .Select(role => role.Id)
            .ToListAsync())
            .ToHashSet();

        foreach (var roleId in requestedRoleIds)
        {
            if (!existingRoleIds.Contains(roleId))
                return ServiceResult<UserDto>.Failure(
                    $"Role ID {roleId} does not exist.");
        }

        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            PasswordHash = PasswordHashing.Hash(request.Password),
            SecurityStamp = SecurityStamps.New(),
            Specialization = request.Specialization,
            LicenseNumber = request.LicenseNumber,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdByUserId
        };

        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync();

        // Assign roles
        foreach (var roleId in requestedRoleIds)
        {
            await _uow.UserRoles.AddAsync(new UserRole
            {
                UserId = user.Id,
                RoleId = roleId,
                AssignedAt = DateTime.UtcNow,
                AssignedBy = createdByUserId
            });
        }
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
        user.SecurityStamp = SecurityStamps.New();   // ends every session, including this one
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
                Roles = u.UserRoles
                    .OrderBy(ur => ur.RoleId)
                    .Select(ur => ur.Role.RoleName)
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
                Roles = u.UserRoles
                    .OrderBy(ur => ur.RoleId)
                    .Select(ur => ur.Role.RoleName)
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
        // Doctor role = ID 2, Nutritionist = ID 5
        var doctors = await _uow.Users
            .Query()
            .Where(u => u.IsActive &&
                        u.UserRoles.Any(ur => ur.RoleId == 2 || ur.RoleId == 5))
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
            .ToListAsync();

        return ServiceResult<IEnumerable<UserDto>>.Success(doctors);
    }

    private const string AdminRoleName = "Admin";
    private const string LastAdminMessage =
        "This is the last active administrator. Give another active user the Admin role first.";

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
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();

        return ServiceResult.Success(isActive ? "User activated." : "User deactivated and signed out.");
    }

    public async Task<ServiceResult> AssignRoleAsync(int userId, int roleId, int assignedBy)
    {
        var roles = _uow.Roles.Query();

        var validation = await _uow.Users
            .Query()
            .Where(u => u.Id == userId)
            .Select(_ => new
            {
                RoleExists = roles.Any(r => r.Id == roleId)
            })
            .FirstOrDefaultAsync();

        if (validation is null)
            return ServiceResult.Failure("User not found.");

        if (!validation.RoleExists)
            return ServiceResult.Failure("Role not found.");

        var roleAlreadyAssigned = await _uow.UserRoles
     .Query()
     .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (roleAlreadyAssigned)
            return ServiceResult.Failure("User already has this role.");

        await _uow.UserRoles.AddAsync(new UserRole
        {
            UserId = userId,
            RoleId = roleId,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = assignedBy
        });
        await RenewStampAsync(userId);   // the user signs in again and gets the new role
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Role assigned. The user will be asked to sign in again.");
    }

    public async Task<ServiceResult> RemoveRoleAsync(int userId, int roleId)
    {
        var userRole = await _uow.UserRoles
            .Query(asNoTracking: false)
            .Include(ur => ur.Role)
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (userRole is null)
            return ServiceResult.Failure("User does not have this role.");

        if (userRole.Role.RoleName == AdminRoleName && await IsLastActiveAdminAsync(userId))
            return ServiceResult.Failure(LastAdminMessage);

        _uow.UserRoles.Delete(userRole);
        await RenewStampAsync(userId);   // the removed role stops working at once
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Role removed. The user will be asked to sign in again.");
    }

    private async Task<bool> IsLastActiveAdminAsync(int userId)
    {
        var activeAdminIds = await _uow.UserRoles
            .Query()
            .Where(ur => ur.Role.RoleName == AdminRoleName && ur.User.IsActive)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync();

        return activeAdminIds.Count == 1 && activeAdminIds[0] == userId;
    }

    private async Task RenewStampAsync(int userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null) return;

        user.SecurityStamp = SecurityStamps.New();
        _uow.Users.Update(user);
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
                    Roles = u.UserRoles
                        .OrderBy(ur => ur.RoleId)
                        .Select(ur => ur.Role.RoleName)
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
            Roles = Enumerable.Empty<string>()
        };
    }
}