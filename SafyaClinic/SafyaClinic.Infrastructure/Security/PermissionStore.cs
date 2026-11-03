using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Security;
using SafyaClinic.Infrastructure.Data;

namespace SafyaClinic.Infrastructure.Security;

/// <summary>F4b: effective permissions from the groups of the user's roles and direct memberships (F4d).</summary>
public sealed class PermissionStore : IPermissionStore
{
    private static readonly IReadOnlySet<string> None = new HashSet<string>();
    private readonly SafyaDbContext _db;

    public PermissionStore(SafyaDbContext db) => _db = db;

    public async Task<IReadOnlySet<string>> GetPermissionsAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId && u.IsActive, cancellationToken))
            return None;

        var codes = await _db.GroupPermissions
            .Where(gp => gp.Group.Members.Any(m => m.UserId == userId))
            .Select(gp => gp.PermissionCode)
            .Distinct()
            .ToListAsync(cancellationToken);

        return codes.ToHashSet(StringComparer.Ordinal);
    }
}