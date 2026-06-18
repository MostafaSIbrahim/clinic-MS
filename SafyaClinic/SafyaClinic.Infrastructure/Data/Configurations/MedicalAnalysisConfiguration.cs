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
    internal class MedicalAnalysisConfiguration : IEntityTypeConfiguration<MedicalAnalysis>
    {
        public void Configure(EntityTypeBuilder<MedicalAnalysis> builder)
        {
            builder.ToTable("MedicalAnalyses");
            builder.HasKey(ma => ma.Id);
            builder.Property(ma => ma.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(ma => ma.ResultNotes).HasMaxLength(2000);

            builder.HasIndex(ma => ma.PatientId);
            builder.HasIndex(ma => ma.Status);

            builder.HasOne(ma => ma.Patient)
                   .WithMany(p => p.Analyses)
                   .HasForeignKey(ma => ma.PatientId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(ma => ma.Doctor)
                   .WithMany(u => u.)
                   .HasForeignKey(ma => ma.DoctorId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
