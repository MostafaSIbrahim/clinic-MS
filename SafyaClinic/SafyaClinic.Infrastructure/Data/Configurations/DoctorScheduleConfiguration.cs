using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Scheduling;

namespace SafyaClinic.Infrastructure.Data.Configurations;

/// <summary>P1: one schedule per doctor and clinic. Removed with its clinic; a doctor with a schedule can't be deleted.</summary>
public class DoctorScheduleConfiguration : IEntityTypeConfiguration<DoctorSchedule>
{
    public void Configure(EntityTypeBuilder<DoctorSchedule> builder)
    {
        builder.ToTable("DoctorSchedules", table =>
            table.HasCheckConstraint("CK_DoctorSchedules_Step", "[StepMinutes] BETWEEN 5 AND 60"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Mode).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(s => new { s.DoctorId, s.ClinicId }).IsUnique();
        builder.HasOne(s => s.Doctor).WithMany().HasForeignKey(s => s.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Clinic).WithMany().HasForeignKey(s => s.ClinicId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>P1: the periods of a schedule's weekly pattern.</summary>
public class DoctorScheduleTimeConfiguration : IEntityTypeConfiguration<DoctorScheduleTime>
{
    public void Configure(EntityTypeBuilder<DoctorScheduleTime> builder)
    {
        builder.ToTable("DoctorScheduleTimes", table =>
            table.HasCheckConstraint("CK_DoctorScheduleTimes_Period", "[StartTime] < [EndTime]"));
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Kind).HasConversion<string>().HasMaxLength(10);
        builder.Property(t => t.StartTime).HasColumnType("time(0)");
        builder.Property(t => t.EndTime).HasColumnType("time(0)");
        builder.HasIndex(t => new { t.ScheduleId, t.DayOfWeek });
        builder.HasOne(t => t.Schedule).WithMany(s => s.Times).HasForeignKey(t => t.ScheduleId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>P1c: date changes of a schedule. Removed with their schedule.</summary>
public class DoctorScheduleChangeConfiguration : IEntityTypeConfiguration<DoctorScheduleChange>
{
    public void Configure(EntityTypeBuilder<DoctorScheduleChange> builder)
    {
        builder.ToTable("DoctorScheduleChanges", table => table.HasCheckConstraint("CK_DoctorScheduleChanges_Period",
            "([IsDayOff] = 1 AND [Kind] IS NULL AND [StartTime] IS NULL AND [EndTime] IS NULL) OR " +
            "([IsDayOff] = 0 AND [Kind] IS NOT NULL AND [StartTime] < [EndTime])"));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Date).HasColumnType("date");
        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(10);
        builder.Property(c => c.StartTime).HasColumnType("time(0)");
        builder.Property(c => c.EndTime).HasColumnType("time(0)");
        builder.HasIndex(c => new { c.ScheduleId, c.Date });
        builder.HasOne(c => c.Schedule).WithMany(s => s.Changes).HasForeignKey(c => c.ScheduleId).OnDelete(DeleteBehavior.Cascade);
    }
}
