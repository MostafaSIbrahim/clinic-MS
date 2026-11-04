using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Settings;

namespace SafyaClinic.Infrastructure.Data.Configurations;

/// <summary>P1: clinic opening periods per weekday (clinic time). Removed with their clinic.</summary>
public class ClinicOpeningHoursConfiguration : IEntityTypeConfiguration<ClinicOpeningHours>
{
    public void Configure(EntityTypeBuilder<ClinicOpeningHours> builder)
    {
        builder.ToTable("ClinicOpeningHours", table =>
            table.HasCheckConstraint("CK_ClinicOpeningHours_Period", "[OpensAt] < [ClosesAt]"));
        builder.HasKey(h => h.Id);
        builder.Property(h => h.OpensAt).HasColumnType("time(0)");
        builder.Property(h => h.ClosesAt).HasColumnType("time(0)");
        builder.HasIndex(h => new { h.ClinicId, h.DayOfWeek });
        builder.HasOne(h => h.Clinic)
            .WithMany(c => c.OpeningHours)
            .HasForeignKey(h => h.ClinicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
