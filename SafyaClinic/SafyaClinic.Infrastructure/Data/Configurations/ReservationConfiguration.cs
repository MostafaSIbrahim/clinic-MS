

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Reservation;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
    {
        public void Configure(EntityTypeBuilder<Reservation> builder)
        {
            builder.ToTable("Reservations");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Reason).HasMaxLength(500);
            builder.Property(r => r.Notes).HasMaxLength(1000);
            builder.Property(r => r.TotalAmount).HasPrecision(18, 2);
            builder.Property(r => r.Category).HasConversion<string>().HasMaxLength(20);

            builder.HasIndex(r => r.PatientId);
            builder.HasIndex(r => r.DoctorId);
            builder.HasIndex(r => r.ReservationDate);
            builder.HasIndex(r => r.StatusId);
            builder.HasIndex(r => r.Category);

            builder.HasOne(r => r.Patient)
                   .WithMany(p => p.Reservations)
                   .HasForeignKey(r => r.PatientId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(r => r.Doctor)
                   .WithMany(u => u.DoctorReservations)
                   .HasForeignKey(r => r.DoctorId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(r => r.Status)
                   .WithMany(rs => rs.Reservations)
                   .HasForeignKey(r => r.StatusId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
