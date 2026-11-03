using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Identity;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    // F4a (D1): permissions, groups, grants and memberships.

    public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
    {
        public void Configure(EntityTypeBuilder<Permission> builder)
        {
            builder.ToTable("Permissions");
            builder.HasKey(p => p.Code);
            builder.Property(p => p.Code).HasMaxLength(100);
            builder.Property(p => p.Area).IsRequired().HasMaxLength(50);
            builder.Property(p => p.Description).IsRequired().HasMaxLength(200);
        }
    }

    public class PermissionGroupConfiguration : IEntityTypeConfiguration<PermissionGroup>
    {
        public void Configure(EntityTypeBuilder<PermissionGroup> builder)
        {
            builder.ToTable("PermissionGroups");
            builder.HasKey(g => g.Id);
            builder.Property(g => g.Name).IsRequired().HasMaxLength(100);
            builder.Property(g => g.Description).HasMaxLength(300);
            builder.Property(g => g.SystemKey).HasMaxLength(50);
            builder.HasIndex(g => g.Name).IsUnique();
            builder.HasIndex(g => g.SystemKey).IsUnique();   // SQL Server filters it to non-null values
        }
    }

    public class GroupPermissionConfiguration : IEntityTypeConfiguration<GroupPermission>
    {
        public void Configure(EntityTypeBuilder<GroupPermission> builder)
        {
            builder.ToTable("GroupPermissions");
            builder.HasKey(gp => new { gp.GroupId, gp.PermissionCode });
            builder.Property(gp => gp.PermissionCode).HasMaxLength(100);

            builder.HasOne(gp => gp.Group)
                   .WithMany(g => g.GroupPermissions)
                   .HasForeignKey(gp => gp.GroupId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(gp => gp.Permission)
                   .WithMany(p => p.GroupPermissions)
                   .HasForeignKey(gp => gp.PermissionCode)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class UserGroupConfiguration : IEntityTypeConfiguration<UserGroup>
    {
        public void Configure(EntityTypeBuilder<UserGroup> builder)
        {
            builder.ToTable("UserGroups");
            builder.HasKey(ug => new { ug.UserId, ug.GroupId });

            builder.HasOne(ug => ug.User)
                   .WithMany(u => u.UserGroups)
                   .HasForeignKey(ug => ug.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(ug => ug.Group)
                   .WithMany(g => g.Members)
                   .HasForeignKey(ug => ug.GroupId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}