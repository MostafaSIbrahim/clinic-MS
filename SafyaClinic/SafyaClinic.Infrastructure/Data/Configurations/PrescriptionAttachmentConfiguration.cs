using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Prescription;

public class PrescriptionAttachmentConfiguration : IEntityTypeConfiguration<PrescriptionAttachment>
{
    public void Configure(EntityTypeBuilder<PrescriptionAttachment> builder)
    {
        builder.ToTable("PrescriptionAttachments");
        builder.HasKey(pa => pa.Id);
        builder.Property(pa => pa.FileName).IsRequired().HasMaxLength(255);
        builder.Property(pa => pa.FilePath).IsRequired().HasMaxLength(500);

        builder.HasOne(pa => pa.Prescription)
               .WithMany(p => p.Attachments)
               .HasForeignKey(pa => pa.PrescriptionId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}