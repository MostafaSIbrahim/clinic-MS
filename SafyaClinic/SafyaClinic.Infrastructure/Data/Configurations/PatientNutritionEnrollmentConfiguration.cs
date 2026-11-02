using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Nutrition;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class PatientNutritionEnrollmentConfiguration : IEntityTypeConfiguration<PatientNutritionEnrollment>
    {
        public void Configure(EntityTypeBuilder<PatientNutritionEnrollment> builder)
        {
            builder.ToTable("PatientNutritionEnrollments");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.BasePrice).HasPrecision(18, 2);
            builder.Property(e => e.DiscountPercent).HasPrecision(5, 2);
            builder.Property(e => e.TotalPaid).HasPrecision(18, 2);
            builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            builder.HasIndex(e => e.PatientId);
            builder.HasIndex(e => e.Status);

            // F5a: enrollments are financial history — never deleted as a side effect.
            builder.HasOne(e => e.Patient)
                   .WithMany(p => p.NutritionEnrollments)
                   .HasForeignKey(e => e.PatientId)
                   .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(e => e.Package)
                   .WithMany(p => p.Enrollments)
                   .HasForeignKey(e => e.PackageId)
                   .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(e => e.Doctor)
                   .WithMany()
                   .HasForeignKey(e => e.DoctorId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
