using SafyaClinic.Application.Security;
using SafyaClinic.Web.Security;

namespace SafyaClinic.Tests.Web.Security;

/// <summary>Test double: the permissions a user with these roles gets from the system groups (F4a).</summary>
public sealed class RolePermissions : ICurrentPermissions
{
    private readonly HashSet<string> _codes;

    public RolePermissions(IEnumerable<string> roles) => _codes = Of(roles);

    public Task<bool> HasAsync(string code) => Task.FromResult(_codes.Contains(code));

    public static HashSet<string> Of(IEnumerable<string> roles) =>
        roles.SelectMany(role => SystemGroups.All.Single(g => g.RoleName == role).Permissions).ToHashSet();
}