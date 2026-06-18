using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Identity;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.ToTable("Roles");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.RoleName).IsRequired().HasMaxLength(50);
            builder.Property(r => r.Description).HasMaxLength(255);
            builder.HasIndex(r => r.RoleName).IsUnique();
        }
    }
}
