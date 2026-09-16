using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Domain.Interfaces.Repositories;

namespace SafyaClinic.Application.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;

    public UserService(IUnitOfWork uow) => _uow = uow;

    public async Task<ServiceResult<UserDto>> CreateUserAsync(
        CreateUserRequest request, int createdByUserId)
    {
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
            PasswordHash = AuthService.HashPassword(request.Password),
            Specialization = request.Specialization,
            LicenseNumber = request.LicenseNumber,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdByUserId
        };

        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync();

        // Assign roles
        foreach (var roleId in request.RoleIds)
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

    public async Task<ServiceResult> SetUserActiveAsync(int userId, bool isActive)
    {
        var affectedRows = await _uow.Users
            .Query()
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.IsActive, isActive));

        return affectedRows == 0
            ? ServiceResult.Failure("User not found.")
            : ServiceResult.Success();
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
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Role assigned.");
    }

    public async Task<ServiceResult> RemoveRoleAsync(int userId, int roleId)
    {
        var affectedRows = await _uow.UserRoles
            .Query()
            .Where(userRole => userRole.UserId == userId && userRole.RoleId == roleId)
            .ExecuteDeleteAsync();

        return affectedRows == 0
            ? ServiceResult.Failure("User does not have this role.")
            : ServiceResult.Success("Role removed.");
    }

    // ── Mapper ────────────────────────────────────────────────

    private async Task<UserDto> MapUserDtoAsync(User user)
    {
        var userRoles = await _uow.UserRoles.FindAsync(ur => ur.UserId == user.Id);
        var roleIds = userRoles.Select(ur => ur.RoleId).ToList();
        var roles = await _uow.Roles.FindAsync(r => roleIds.Contains(r.Id));

        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Specialization = user.Specialization,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt,
            Roles = roles.Select(r => r.RoleName)
        };
    }
}