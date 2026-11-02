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

        // F5a: name the collection (removes the shadow PatientRecordId) and protect clinical history.
        builder.HasOne(p => p.Record)
               .WithMany(r => r.Prescriptions)
               .HasForeignKey(p => p.RecordId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}