using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Audit;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.ToTable("AuditLogs");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Action).IsRequired().HasMaxLength(64);
            builder.Property(a => a.EntityType).IsRequired().HasMaxLength(64);
            builder.Property(a => a.Details).HasMaxLength(4000);
            builder.Property(a => a.IpAddress).HasMaxLength(45);
            builder.Property(a => a.RequestId).HasMaxLength(64);
            builder.HasIndex(a => a.OccurredAtUtc);
            builder.HasIndex(a => new { a.EntityType, a.EntityId });
            builder.HasIndex(a => new { a.UserId, a.OccurredAtUtc });
        }
    }
}