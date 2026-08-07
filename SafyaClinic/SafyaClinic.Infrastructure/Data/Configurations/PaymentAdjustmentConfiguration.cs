using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Payment;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class PaymentAdjustmentConfiguration : IEntityTypeConfiguration<PaymentAdjustment>
    {
        public void Configure(EntityTypeBuilder<PaymentAdjustment> builder)
        {
            builder.ToTable("PaymentAdjustments");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.ActionType).IsRequired().HasMaxLength(30);
            builder.Property(a => a.OldAmount).HasPrecision(18, 2);
            builder.Property(a => a.NewAmount).HasPrecision(18, 2);
            builder.Property(a => a.Reason).HasMaxLength(500);

            builder.HasIndex(a => a.PaymentId);

            builder.HasOne(a => a.Payment)
                   .WithMany(p => p.Adjustments)
                   .HasForeignKey(a => a.PaymentId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
