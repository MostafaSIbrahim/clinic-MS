using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Payment;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.ToTable("Payments");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Amount).HasPrecision(18, 2);
            builder.Property(p => p.ReferenceNumber).HasMaxLength(100);
            builder.Property(p => p.Notes).HasMaxLength(500);
            builder.Property(p => p.PaymentMethod).HasConversion<string>().HasMaxLength(20);
            builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(p => p.DeductionPercentage).HasPrecision(5, 2);
            builder.Property(p => p.SourceDeductionAmount).HasPrecision(18, 2);
            builder.Property(p => p.ClinicNetAmount).HasPrecision(18, 2);
            builder.Property(p => p.OriginalAmount).HasPrecision(18, 2);
            builder.Property(p => p.CancellationReason).HasMaxLength(500);

            builder.HasIndex(p => p.PatientId);
            builder.HasIndex(p => p.ReservationId);
            builder.HasIndex(p => p.ClinicId);
            builder.HasIndex(p => p.PatientSourceId);
            builder.HasIndex(p => p.Status);

            builder.HasOne(p => p.Patient)
                   .WithMany(pt => pt.Payments)
                   .HasForeignKey(p => p.PatientId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.Clinic)
                   .WithMany(c => c.Payments)
                   .HasForeignKey(p => p.ClinicId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.PatientSource)
                   .WithMany(s => s.Payments)
                   .HasForeignKey(p => p.PatientSourceId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
