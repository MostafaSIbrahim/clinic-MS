using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Prescription;

public class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.ToTable("Prescriptions");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PrescriptionDate).IsRequired();
        builder.Property(p => p.Notes).HasMaxLength(1000);

        builder.HasOne(p => p.Record)
               .WithMany()
               .HasForeignKey(p => p.RecordId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}