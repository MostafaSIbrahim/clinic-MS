using SafyaClinic.Domain.Entities.Common;

namespace SafyaClinic.Domain.Identity
{
    /// <summary>F4a (D1): an action the code can enforce. Defined in code (PermissionCatalogue); the seeder copies it here.</summary>
    public class Permission
    {
        public string Code { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSensitive { get; set; }

        public virtual ICollection<GroupPermission> GroupPermissions { get; set; } = new List<GroupPermission>();
    }

    /// <summary>F4a: a named set of permissions. Admins manage groups from F4d.</summary>
    public class PermissionGroup : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        /// <summary>The role this group stands for until roles are retired (F4d); null for groups an admin creates.</summary>
        public string? SystemKey { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }

        public virtual ICollection<GroupPermission> GroupPermissions { get; set; } = new List<GroupPermission>();
        public virtual ICollection<UserGroup> Members { get; set; } = new List<UserGroup>();
    }

    /// <summary>F4a: a permission granted to a group.</summary>
    public class GroupPermission
    {
        public int GroupId { get; set; }
        public string PermissionCode { get; set; } = string.Empty;

        public virtual PermissionGroup Group { get; set; } = null!;
        public virtual Permission Permission { get; set; } = null!;
    }

    /// <summary>F4a: a user's membership of a group. Empty until F4d; until then membership comes from the role.</summary>
    public class UserGroup
    {
        public int UserId { get; set; }
        public int GroupId { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public int? AssignedBy { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual PermissionGroup Group { get; set; } = null!;
    }
}