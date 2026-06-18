using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Prescription;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class PrescriptionAttachmentConfiguration : IEntityTypeConfiguration<PrescriptionAttachment>
    {
        public void Configure(EntityTypeBuilder<PrescriptionAttachment> builder)
        {
            builder.ToTable("PrescriptionAttachments");
            builder.HasKey(pa => pa.Id);
            builder.Property(pa => pa.FileName).IsRequired().HasMaxLength(255);
            builder.Property(pa => pa.FilePath).IsRequired().HasMaxLength(500);
            builder.Property(pa => pa.FileType).HasMaxLength(50);

            builder.HasOne(pa => pa.Prescription)
                   .WithMany(p => p.Attachments)
                   .HasForeignKey(pa => pa.PrescriptionId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
