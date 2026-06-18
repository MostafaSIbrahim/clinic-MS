using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Analysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class AnalysisAttachmentConfiguration : IEntityTypeConfiguration<AnalysisAttachment>
    {
        public void Configure(EntityTypeBuilder<AnalysisAttachment> builder)
        {
            builder.ToTable("AnalysisAttachments");
            builder.HasKey(aa => aa.Id);
            builder.Property(aa => aa.FileName).IsRequired().HasMaxLength(255);
            builder.Property(aa => aa.FilePath).IsRequired().HasMaxLength(500);
            builder.Property(aa => aa.FileType).HasMaxLength(50);

            builder.HasOne(aa => aa.Analysis)
                   .WithMany(ma => ma.Attachments)
                   .HasForeignKey(aa => aa.AnalysisId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
