using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Patient;


namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class PatientConfiguration : IEntityTypeConfiguration<Patient>
    {
        public void Configure(EntityTypeBuilder<Patient> builder)
        {
            builder.ToTable("Patients");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.FirstName).IsRequired().HasMaxLength(50);
            builder.Property(p => p.LastName).IsRequired().HasMaxLength(50);
            builder.Property(p => p.NationalId).HasMaxLength(20);
            builder.Property(p => p.HeightCm).HasPrecision(5, 2);
            builder.Property(p => p.Allergies).HasMaxLength(500);
            builder.Property(p => p.ChronicDiseases).HasMaxLength(500);
            builder.Property(p => p.Notes).HasMaxLength(1000);

            builder.HasIndex(p => p.NationalId).IsUnique();
            builder.HasIndex(p => p.UserId);

            builder.HasOne(p => p.User)
                   .WithMany(u => u.CreatedPatients)
                   .HasForeignKey(p => p.UserId)
                   .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
