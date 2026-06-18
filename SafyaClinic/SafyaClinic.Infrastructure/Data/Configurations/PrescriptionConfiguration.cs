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
    public class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
    {
        public void Configure(EntityTypeBuilder<Prescription> builder)
        {
            builder.ToTable("Prescriptions");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.MedicationName).IsRequired().HasMaxLength(200);
            builder.Property(p => p.Dosage).HasMaxLength(100);
            builder.Property(p => p.Frequency).HasMaxLength(100);
            builder.Property(p => p.Duration).HasMaxLength(100);
            builder.Property(p => p.RouteOfAdministration).HasMaxLength(50);
            builder.Property(p => p.Instructions).HasMaxLength(1000);

            builder.HasOne(p => p.Record)
                   .WithMany(pr => pr.Prescriptions)
                   .HasForeignKey(p => p.RecordId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
