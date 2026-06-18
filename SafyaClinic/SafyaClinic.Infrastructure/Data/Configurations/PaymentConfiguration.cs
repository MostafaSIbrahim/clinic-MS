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

            builder.HasIndex(p => p.PatientId);
            builder.HasIndex(p => p.ReservationId);

            builder.HasOne(p => p.Patient)
                   .WithMany(pt => pt.Payments)
                   .HasForeignKey(p => p.PatientId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
