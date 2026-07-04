using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SafyaClinic.Application.DTOs.Auth;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.Patient;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Domain.Interfaces.Repositories;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SafyaClinic.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _config;

    public AuthService(IUnitOfWork uow, IConfiguration config)
    {
        _uow = uow;
        _config = config;
    }

    public async Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request)
    {
        // Find user by phone number
        var user = await _uow.Users.FirstOrDefaultAsync(
            u => u.PhoneNumber == request.PhoneNumber && u.IsActive);

        if (user is null)
            return ServiceResult<LoginResponse>.Failure("Invalid phone number or password.");

        // Verify password (ASP.NET Identity PBKDF2 format)
        if (!VerifyPassword(request.Password, user.PasswordHash))
            return ServiceResult<LoginResponse>.Failure("Invalid phone number or password.");

        // Load roles
        var userRoles = await _uow.UserRoles.FindAsync(ur => ur.UserId == user.Id);
        var roleIds = userRoles.Select(ur => ur.RoleId).ToList();
        var roles = await _uow.Roles.FindAsync(r => roleIds.Contains(r.Id));
        var roleNames = roles.Select(r => r.RoleName).ToList();

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();

        var expiry = DateTime.UtcNow.AddHours(8);
        var token = GenerateJwtToken(user.Id, user.FullName, roleNames, expiry);

        return ServiceResult<LoginResponse>.Success(new LoginResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Token = token,
            ExpiresAt = expiry,
            Roles = roleNames
        });
    }

    public async Task<ServiceResult> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null)
            return ServiceResult.Failure("User not found.");

        if (!VerifyPassword(request.CurrentPassword, user.PasswordHash))
            return ServiceResult.Failure("Current password is incorrect.");

        user.PasswordHash = HashPassword(request.NewPassword);
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();

        return ServiceResult.Success("Password changed successfully.");
    }

    public async Task<ServiceResult<UserDto>> GetCurrentUserAsync(int userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null)
            return ServiceResult<UserDto>.Failure("User not found.");

        var userRoles = await _uow.UserRoles.FindAsync(ur => ur.UserId == userId);
        var roleIds = userRoles.Select(ur => ur.RoleId).ToList();
        var roles = await _uow.Roles.FindAsync(r => roleIds.Contains(r.Id));

        return ServiceResult<UserDto>.Success(new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Specialization = user.Specialization,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt,
            Roles = roles.Select(r => r.RoleName)
        });
    }

    // ── Helpers ──────────────────────────────────────────────

    private string GenerateJwtToken(
        int userId, string fullName, IEnumerable<string> roles, DateTime expiry)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured")));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames.Name, fullName),
            new(Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Simple PBKDF2 password helpers compatible with ASP.NET Identity v3 format.
    // In production replace with Microsoft.AspNetCore.Identity.PasswordHasher<T>.
    public static string HashPassword(string password)
    {
        // Use a well-known library method if available; here we use a SHA256 +
        // salt approach as a stand-in that compiles without ASP.NET Identity.
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        var hash = System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes(password).Concat(salt).ToArray());
        return Convert.ToBase64String(salt) + "." + Convert.ToBase64String(hash);
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            var parts = storedHash.Split('.');
            if (parts.Length != 2) return false;

            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            var actual = System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(password).Concat(salt).ToArray());

            return CryptographicEquals(expected, actual);
        }
        catch { return false; }
    }

    private static bool CryptographicEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}