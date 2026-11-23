using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Settings;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    /// <summary>M5d: the clinic profile (one row at most, enforced by the service).</summary>
    public class ClinicProfileConfiguration : IEntityTypeConfiguration<ClinicProfile>
    {
        public void Configure(EntityTypeBuilder<ClinicProfile> builder)
        {
            builder.ToTable("ClinicProfiles");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.NameEn).IsRequired().HasMaxLength(150);
            builder.Property(p => p.NameAr).HasMaxLength(150);
            builder.Property(p => p.TaglineEn).HasMaxLength(200);
            builder.Property(p => p.TaglineAr).HasMaxLength(200);
            builder.Property(p => p.AddressEn).HasMaxLength(300);
            builder.Property(p => p.AddressAr).HasMaxLength(300);
            builder.Property(p => p.Phone).HasMaxLength(30);
            builder.Property(p => p.SecondPhone).HasMaxLength(30);
            builder.Property(p => p.Email).HasMaxLength(150);
            builder.Property(p => p.LicenceLine).HasMaxLength(200);
        }
    }
}
