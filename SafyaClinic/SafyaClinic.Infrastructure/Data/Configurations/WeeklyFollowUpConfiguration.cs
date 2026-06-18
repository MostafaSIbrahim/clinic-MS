using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Nutrition;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class WeeklyFollowUpConfiguration : IEntityTypeConfiguration<WeeklyFollowUp>
    {
        public void Configure(EntityTypeBuilder<WeeklyFollowUp> builder)
        {
            builder.ToTable("WeeklyFollowUps");
            builder.HasKey(w => w.Id);
            builder.Property(w => w.WeightKg).HasPrecision(5, 2);
            builder.Property(w => w.HeightCm).HasPrecision(5, 2);
            builder.Property(w => w.BMI).HasPrecision(5, 2);
            builder.Property(w => w.BodyFatPercent).HasPrecision(5, 2);
            builder.Property(w => w.MuscleMassKg).HasPrecision(5, 2);
            builder.Property(w => w.WaistCircumferenceCm).HasPrecision(5, 2);
            builder.Property(w => w.DietCompliance).HasConversion<string>().HasMaxLength(20);
            builder.HasIndex(w => w.EnrollmentId);
            builder.HasIndex(w => w.FollowUpDate);
        }
    }
}
