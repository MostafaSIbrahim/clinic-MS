using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Domain.Enums;

namespace SafyaClinic.Application.Interfaces.Services;

public interface IUserService
{
    Task<ServiceResult<UserDto>> CreateUserAsync(CreateUserRequest request, int createdByUserId);
    Task<ServiceResult> ResetPasswordAsync(ResetPasswordRequest request);
    Task<ServiceResult<UserDto>> GetUserByIdAsync(int userId);
    Task<ServiceResult<PagedResult<UserDto>>> GetAllUsersAsync( 
            PaginationRequest request);
    Task<ServiceResult<IEnumerable<UserDto>>> GetDoctorsAsync();
    Task<ServiceResult> SetUserActiveAsync(int userId, bool isActive, int actingUserId);
    Task<ServiceResult> SetClinicianTypeAsync(int userId, ClinicianType clinicianType, int actingUserId);
}