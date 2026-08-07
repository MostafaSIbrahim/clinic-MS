using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Settings;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class ClinicSourceAgreementConfiguration : IEntityTypeConfiguration<ClinicSourceAgreement>
    {
        public void Configure(EntityTypeBuilder<ClinicSourceAgreement> builder)
        {
            builder.ToTable("ClinicSourceAgreements");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.DeductionPercentage).HasPrecision(5, 2);
            builder.Property(a => a.Notes).HasMaxLength(500);

            // One agreement per Clinic + PatientSource pair
            builder.HasIndex(a => new { a.ClinicId, a.PatientSourceId }).IsUnique();

            builder.HasOne(a => a.Clinic)
                   .WithMany(c => c.SourceAgreements)
                   .HasForeignKey(a => a.ClinicId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(a => a.PatientSource)
                   .WithMany(s => s.ClinicAgreements)
                   .HasForeignKey(a => a.PatientSourceId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
