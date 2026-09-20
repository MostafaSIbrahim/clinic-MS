using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.MedicalRecord;
using SafyaClinic.Domain.Identity;
using SafyaClinic.Domain.Enums;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class PatientRecordConfiguration : IEntityTypeConfiguration<PatientRecord>
    {
        public void Configure(EntityTypeBuilder<PatientRecord> builder)
        {
            builder.ToTable("PatientRecords");
            builder.HasKey(pr => pr.Id);
            builder.Property(pr => pr.ChiefComplaint).HasMaxLength(500);
            builder.Property(pr => pr.PresentIllnessHistory).HasMaxLength(2000);
            builder.Property(pr => pr.Diagnosis).HasMaxLength(1000);
            builder.Property(pr => pr.DifferentialDiagnosis).HasMaxLength(1000);
            builder.Property(pr => pr.TreatmentPlan).HasMaxLength(2000);
            builder.Property(pr => pr.Notes).HasMaxLength(2000);
            builder.Property(pr => pr.Category).HasConversion<string>().HasMaxLength(20);
            builder.Property(pr => pr.FollowUpStatus)
                        .HasConversion<string>()
                        .HasMaxLength(20)
                        .HasDefaultValue(PatientFollowUpStatus.Pending);

            builder.Property(pr => pr.FollowUpDismissalReason)
                       .HasMaxLength(500);

            builder.HasIndex(pr => new
            {
                pr.FollowUpStatus,
                pr.FollowUpDate,
                pr.DoctorId
            });

            builder.HasOne(pr => pr.Reservation)
                .WithOne(r => r.PatientRecord)
                .HasForeignKey<PatientRecord>(pr => pr.ReservationId)
                .OnDelete(DeleteBehavior.ClientSetNull);
            builder.HasOne(pr => pr.FollowUpReservation)
                .WithMany()
                .HasForeignKey(pr => pr.FollowUpReservationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(pr => pr.FollowUpUpdatedBy)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(pr => pr.PatientId);
            builder.HasIndex(pr => pr.DoctorId);

            builder.HasOne(pr => pr.Patient)
                   .WithMany(p => p.Records)
                   .HasForeignKey(pr => pr.PatientId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(pr => pr.Doctor)
                   .WithMany(u => u.DoctorRecords)
                   .HasForeignKey(pr => pr.DoctorId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<User>()
                   .WithMany()
                   .HasForeignKey(pr => pr.CreatedBy)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
