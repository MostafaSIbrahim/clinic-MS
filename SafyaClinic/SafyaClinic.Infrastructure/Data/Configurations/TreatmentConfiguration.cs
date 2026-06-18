using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.MedicalRecord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class TreatmentConfiguration : IEntityTypeConfiguration<Treatment>
    {
        public void Configure(EntityTypeBuilder<Treatment> builder)
        {
            builder.ToTable("Treatments");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Description).IsRequired().HasMaxLength(1000);
            builder.Property(t => t.Cost).HasPrecision(18, 2);
            builder.Property(t => t.Notes).HasMaxLength(500);

            builder.HasOne(t => t.Record)
                   .WithMany(pr => pr.Treatments)
                   .HasForeignKey(t => t.RecordId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
