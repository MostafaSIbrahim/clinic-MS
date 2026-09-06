using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Prescription;

public class PrescriptionItemConfiguration : IEntityTypeConfiguration<PrescriptionItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionItem> builder)
    {
        builder.ToTable("PrescriptionItems");
        builder.HasKey(pi => pi.Id);
        builder.Property(pi => pi.MedicationName).IsRequired().HasMaxLength(200);
        builder.Property(pi => pi.Dosage).HasMaxLength(100);
        builder.Property(pi => pi.Frequency).HasMaxLength(100);
        builder.Property(pi => pi.Duration).HasMaxLength(100);
        builder.Property(pi => pi.RouteOfAdministration).HasMaxLength(50);
        builder.Property(pi => pi.Instructions).HasMaxLength(1000);

        builder.HasOne(pi => pi.Prescription)
               .WithMany(p => p.Items)
               .HasForeignKey(pi => pi.PrescriptionId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}