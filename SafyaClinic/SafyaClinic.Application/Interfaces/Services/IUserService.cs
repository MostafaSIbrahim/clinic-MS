using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Patient;

namespace SafyaClinic.Application.Interfaces.Services;

public interface IUserService
{
    Task<ServiceResult<UserDto>> CreateUserAsync(CreateUserRequest request, int createdByUserId);
    Task<ServiceResult<UserDto>> GetUserByIdAsync(int userId);
    Task<ServiceResult<IEnumerable<UserDto>>> GetAllUsersAsync();
    Task<ServiceResult<IEnumerable<UserDto>>> GetDoctorsAsync();
    Task<ServiceResult> SetUserActiveAsync(int userId, bool isActive);
    Task<ServiceResult> AssignRoleAsync(int userId, int roleId, int assignedBy);
    Task<ServiceResult> RemoveRoleAsync(int userId, int roleId);
}