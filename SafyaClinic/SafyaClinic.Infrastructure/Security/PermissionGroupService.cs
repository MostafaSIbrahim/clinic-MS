using Microsoft.EntityFrameworkCore;
using SafyaClinic.Application.Common.Auditing;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.Security;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Infrastructure.Data;

namespace SafyaClinic.Infrastructure.Security;

/// <summary>F4d: permission groups, their grants and direct members. Every change is audited.</summary>
public sealed class PermissionGroupService : IPermissionGroupService
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 300;
    private const string EntityType = "PermissionGroup";

    private readonly SafyaDbContext _db;
    private readonly IAuditTrail _audit;

    public PermissionGroupService(SafyaDbContext db, IAuditTrail? audit = null)
    {
        _db = db;
        _audit = audit ?? NoAuditTrail.Instance;
    }

    public async Task<IReadOnlyList<PermissionGroupSummary>> GetGroupsAsync()
    {
        var groups = await _db.PermissionGroups.AsNoTracking()
            .Select(g => new
            {
                g.Id, g.Name, g.Description, g.SystemKey,
                Permissions = g.GroupPermissions.Count(),
                Direct = g.Members.Count()
            })
            .ToListAsync();

        return groups
            .Select(g => new PermissionGroupSummary(g.Id, g.Name, g.Description, g.SystemKey, g.Permissions, g.Direct))
            .OrderBy(g => g.SystemKey is null)
            .ThenBy(g => g.Name)
            .ToList();
    }

    public async Task<PermissionGroupDetail?> GetGroupAsync(int id)
    {
        var group = await _db.PermissionGroups.AsNoTracking()
            .Where(g => g.Id == id)
            .Select(g => new { g.Id, g.Name, g.Description, g.SystemKey })
            .FirstOrDefaultAsync();
        if (group is null) return null;

        var codes = await _db.GroupPermissions.AsNoTracking()
            .Where(gp => gp.GroupId == id)
            .Select(gp => gp.PermissionCode)
            .ToListAsync();

        var members = await _db.UserGroups.AsNoTracking()
            .Where(m => m.GroupId == id)
            .OrderBy(m => m.User.FullName)
            .Select(m => new GroupMember(m.UserId, m.User.FullName, m.User.IsActive))
            .ToListAsync();
        return new PermissionGroupDetail(group.Id, group.Name, group.Description, group.SystemKey,
            codes.ToHashSet(StringComparer.Ordinal), members);
    }

    public async Task<IReadOnlyList<UserChoice>> GetUserChoicesAsync() =>
        await _db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new UserChoice(u.Id, u.FullName))
            .ToListAsync();

    public async Task<ServiceResult<int>> CreateGroupAsync(string name, string? description, int actingUserId)
    {
        name = name?.Trim() ?? string.Empty;
        description = Clean(description);
        var problem = await NameProblemAsync(name, null) ?? DescriptionProblem(description);
        if (problem is not null) return ServiceResult<int>.Failure(problem);

        var group = new PermissionGroup
        {
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actingUserId
        };
        _db.PermissionGroups.Add(group);
        await _db.SaveChangesAsync();

        await _audit.RecordAsync(AuditActions.GroupCreated, EntityType, group.Id, new { name }, actingUserId);
        await _db.SaveChangesAsync();
        return ServiceResult<int>.Success(group.Id, "Group created. Now choose its permissions and members.");
    }

    public async Task<ServiceResult> UpdateGroupAsync(
        int id, string name, string? description, IEnumerable<string> permissionCodes, int actingUserId)
    {
        var group = await _db.PermissionGroups
            .Include(g => g.GroupPermissions)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (group is null) return ServiceResult.Failure("Group not found.");

        var current = group.GroupPermissions.Select(p => p.PermissionCode).ToHashSet(StringComparer.Ordinal);
        var requested = (permissionCodes ?? Enumerable.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        var known = PermissionCatalogue.All.Select(p => p.Code).ToHashSet(StringComparer.Ordinal);
        var unknown = requested.Where(code => !known.Contains(code)).ToList();
        if (unknown.Count > 0)
            return ServiceResult.Failure("Unknown permission: " + string.Join(", ", unknown));

        // Administrators always keep every permission (the seeder restores any that go missing).
        if (group.SystemKey == SystemGroups.AdminRole)
            requested = current;

        if (group.SystemKey is null)
        {
            name = name?.Trim() ?? string.Empty;
            var nameProblem = await NameProblemAsync(name, id);
            if (nameProblem is not null) return ServiceResult.Failure(nameProblem);
            group.Name = name;
        }

        description = Clean(description);
        var descriptionProblem = DescriptionProblem(description);
        if (descriptionProblem is not null) return ServiceResult.Failure(descriptionProblem);
        group.Description = description;

        var added = requested.Except(current).OrderBy(c => c).ToList();
        var removed = current.Except(requested).OrderBy(c => c).ToList();
        foreach (var grant in group.GroupPermissions.Where(p => removed.Contains(p.PermissionCode)).ToList())
            _db.GroupPermissions.Remove(grant);
        foreach (var code in added)
            group.GroupPermissions.Add(new GroupPermission { PermissionCode = code });

        group.UpdatedAt = DateTime.UtcNow;
        group.UpdatedBy = actingUserId;
        await _audit.RecordAsync(AuditActions.GroupUpdated, EntityType, id, new { added, removed }, actingUserId);
        await _db.SaveChangesAsync();

        return ServiceResult.Success(added.Count + removed.Count == 0
            ? "Group saved."
            : $"Group saved: {added.Count} permission(s) granted, {removed.Count} removed. Members get the change within a minute.");
    }

    public async Task<ServiceResult> DeleteGroupAsync(int id, int actingUserId)
    {
        var group = await _db.PermissionGroups
            .Include(g => g.GroupPermissions)
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == id);
        if (group is null) return ServiceResult.Failure("Group not found.");
        if (group.SystemKey is not null)
            return ServiceResult.Failure("Built-in groups cannot be deleted. Remove permissions from them instead.");

        _db.PermissionGroups.Remove(group);   // its grants and memberships go with it
        await _audit.RecordAsync(AuditActions.GroupDeleted, EntityType, id,
            new { group.Name, members = group.Members.Count }, actingUserId);
        await _db.SaveChangesAsync();
        return ServiceResult.Success($"Group \"{group.Name}\" deleted.");
    }

    public async Task<ServiceResult> AddMemberAsync(int groupId, int userId, int actingUserId)
    {
        if (!await _db.PermissionGroups.AnyAsync(g => g.Id == groupId))
            return ServiceResult.Failure("Group not found.");
        if (!await _db.Users.AnyAsync(u => u.Id == userId && u.IsActive))
            return ServiceResult.Failure("Choose an active user.");
        if (await _db.UserGroups.AnyAsync(m => m.GroupId == groupId && m.UserId == userId))
            return ServiceResult.Failure("This user is already a member of the group.");

        _db.UserGroups.Add(new UserGroup
        {
            GroupId = groupId,
            UserId = userId,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = actingUserId
        });
        await _audit.RecordAsync(AuditActions.GroupMemberAdded, EntityType, groupId, new { userId }, actingUserId);
        await _db.SaveChangesAsync();
        return ServiceResult.Success("Member added. Their access changes within a minute.");
    }

    public async Task<ServiceResult> RemoveMemberAsync(int groupId, int userId, int actingUserId)
    {
        var membership = await _db.UserGroups
            .Include(m => m.Group)
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (membership is null) return ServiceResult.Failure("This user is not a member of the group.");

        // At least one active administrator must remain.
        if (membership.Group.SystemKey == SystemGroups.AdminRole)
        {
            var otherActiveAdmins = await _db.UserGroups.AnyAsync(m =>
                m.GroupId == groupId && m.UserId != userId && m.User.IsActive);
            if (!otherActiveAdmins)
                return ServiceResult.Failure("This is the last active administrator. Add another active user to Administrators first.");
        }

        _db.UserGroups.Remove(membership);
        await _audit.RecordAsync(AuditActions.GroupMemberRemoved, EntityType, groupId, new { userId }, actingUserId);
        await _db.SaveChangesAsync();
        return ServiceResult.Success("Member removed. Their access changes within a minute.");
    }

    private async Task<string?> NameProblemAsync(string name, int? exceptId)
    {
        if (name.Length == 0) return "Enter a group name.";
        if (name.Length > MaxNameLength) return $"The group name can have at most {MaxNameLength} characters.";
        var upper = name.ToUpperInvariant();
        var taken = await _db.PermissionGroups.AnyAsync(g => g.Id != exceptId && g.Name.ToUpper() == upper);
        return taken ? "A group with this name already exists." : null;
    }

    private static string? DescriptionProblem(string? description) =>
        description is { Length: > MaxDescriptionLength }
            ? $"The description can have at most {MaxDescriptionLength} characters."
            : null;

    private static string? Clean(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
