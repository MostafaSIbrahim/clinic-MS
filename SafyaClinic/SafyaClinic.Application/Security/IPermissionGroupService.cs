using SafyaClinic.Application.DTOs.Common;

namespace SafyaClinic.Application.Security;

public sealed record PermissionGroupSummary(
    int Id, string Name, string? Description, string? SystemKey, int PermissionCount, int MemberCount);

public sealed record GroupMember(int UserId, string FullName, bool IsActive);

public sealed record PermissionGroupDetail(
    int Id, string Name, string? Description, string? SystemKey,
    IReadOnlySet<string> Permissions, IReadOnlyList<GroupMember> Members)
{
    public bool IsBuiltIn => SystemKey is not null;
    public bool IsAdministrators => SystemKey == SystemGroups.AdminRole;
}

public sealed record UserChoice(int Id, string FullName);

/// <summary>
/// F4d (D1): admins manage permission groups and their members. Built-in groups keep their name and
/// cannot be deleted; Administrators always have every permission and keep at least one active member.
/// </summary>
public interface IPermissionGroupService
{
    Task<IReadOnlyList<PermissionGroupSummary>> GetGroupsAsync();
    Task<PermissionGroupDetail?> GetGroupAsync(int id);
    Task<IReadOnlyList<UserChoice>> GetUserChoicesAsync();
    Task<ServiceResult<int>> CreateGroupAsync(string name, string? description, int actingUserId);
    Task<ServiceResult> UpdateGroupAsync(int id, string name, string? description, IEnumerable<string> permissionCodes, int actingUserId);
    Task<ServiceResult> DeleteGroupAsync(int id, int actingUserId);
    Task<ServiceResult> AddMemberAsync(int groupId, int userId, int actingUserId);
    Task<ServiceResult> RemoveMemberAsync(int groupId, int userId, int actingUserId);
}
