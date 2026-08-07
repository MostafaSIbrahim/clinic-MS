using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Settings;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class PatientSourceConfiguration : IEntityTypeConfiguration<PatientSource>
    {
        public void Configure(EntityTypeBuilder<PatientSource> builder)
        {
            builder.ToTable("PatientSources");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
            builder.Property(s => s.Description).HasMaxLength(500);
            builder.Property(s => s.DefaultDeductionPercentage).HasPrecision(5, 2);

            builder.HasIndex(s => s.Name).IsUnique();
        }
    }
}
