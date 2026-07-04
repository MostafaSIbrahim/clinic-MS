using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Analysis;
using SafyaClinic.Domain.Identity;
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

            builder.Property(ma => ma.Status)
                   .HasConversion<string>()
                   .HasMaxLength(20);

            builder.Property(ma => ma.ResultNotes)
                   .HasMaxLength(2000);

            // Indexes
            builder.HasIndex(ma => ma.PatientId);
            builder.HasIndex(ma => ma.Status);
            builder.HasIndex(ma => ma.DoctorId);
            builder.HasIndex(ma => ma.RecordId);
            builder.HasIndex(ma => ma.AnalysisTypeId);

            // Patient → MedicalAnalysis (1:N)
            builder.HasOne(ma => ma.Patient)
                   .WithMany(p => p.Analyses)
                   .HasForeignKey(ma => ma.PatientId)
                   .OnDelete(DeleteBehavior.Restrict);

            // User (Doctor) → MedicalAnalysis (1:N)
            builder.HasOne(ma => ma.Doctor)
                   .WithMany(u => u.DoctorAnalyses)
                   .HasForeignKey(ma => ma.DoctorId)
                   .OnDelete(DeleteBehavior.Restrict);

            // PatientRecord → MedicalAnalysis (1:N, optional)
            builder.HasOne(ma => ma.Record)
                   .WithMany()
                   .HasForeignKey(ma => ma.RecordId)
                   .OnDelete(DeleteBehavior.Restrict);

            // AnalysisType → MedicalAnalysis (1:N)
            builder.HasOne(ma => ma.Type)
                   .WithMany()
                   .HasForeignKey(ma => ma.AnalysisTypeId)
                   .OnDelete(DeleteBehavior.Restrict);

            // CreatedBy → User (AuditableEntity FK)
            builder.HasOne<User>()
                   .WithMany()
                   .HasForeignKey(ma => ma.CreatedBy)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
