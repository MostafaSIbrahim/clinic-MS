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
        }
    }
}
