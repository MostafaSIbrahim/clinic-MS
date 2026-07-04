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
        foreach (var roleId in request.RoleIds)
            if (!await _uow.Roles.ExistsAsync(roleId))
                return ServiceResult<UserDto>.Failure($"Role ID {roleId} does not exist.");

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
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null)
            return ServiceResult<UserDto>.Failure("User not found.");

        return ServiceResult<UserDto>.Success(await MapUserDtoAsync(user));
    }

    public async Task<ServiceResult<IEnumerable<UserDto>>> GetAllUsersAsync()
    {
        var users = await _uow.Users.GetAllAsync();
        var dtos = new List<UserDto>();
        foreach (var u in users)
            dtos.Add(await MapUserDtoAsync(u));
        return ServiceResult<IEnumerable<UserDto>>.Success(dtos);
    }

    public async Task<ServiceResult<IEnumerable<UserDto>>> GetDoctorsAsync()
    {
        // Doctor role = ID 2, Nutritionist = ID 5
        var doctorRoles = await _uow.UserRoles.FindAsync(ur => ur.RoleId == 2 || ur.RoleId == 5);
        var doctorUserIds = doctorRoles.Select(ur => ur.UserId).Distinct().ToList();
        var doctors = await _uow.Users.FindAsync(u => doctorUserIds.Contains(u.Id) && u.IsActive);

        var dtos = new List<UserDto>();
        foreach (var u in doctors)
            dtos.Add(await MapUserDtoAsync(u));
        return ServiceResult<IEnumerable<UserDto>>.Success(dtos);
    }

    public async Task<ServiceResult> SetUserActiveAsync(int userId, bool isActive)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null)
            return ServiceResult.Failure("User not found.");

        user.IsActive = isActive;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> AssignRoleAsync(int userId, int roleId, int assignedBy)
    {
        if (!await _uow.Users.ExistsAsync(userId))
            return ServiceResult.Failure("User not found.");
        if (!await _uow.Roles.ExistsAsync(roleId))
            return ServiceResult.Failure("Role not found.");

        var existing = await _uow.UserRoles.FirstOrDefaultAsync(
            ur => ur.UserId == userId && ur.RoleId == roleId);
        if (existing is not null)
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
        var userRole = await _uow.UserRoles.FirstOrDefaultAsync(
            ur => ur.UserId == userId && ur.RoleId == roleId);
        if (userRole is null)
            return ServiceResult.Failure("User does not have this role.");

        _uow.UserRoles.Delete(userRole);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Role removed.");
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