using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Patient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class PatientAddressConfiguration : IEntityTypeConfiguration<PatientAddress>
    {
        public void Configure(EntityTypeBuilder<PatientAddress> builder)
        {
            builder.ToTable("PatientAddresses");
            builder.HasKey(pa => pa.Id);
            builder.Property(pa => pa.Street).HasMaxLength(200);
            builder.Property(pa => pa.City).IsRequired().HasMaxLength(50);
            builder.Property(pa => pa.Governorate).HasMaxLength(50);
            builder.Property(pa => pa.PostalCode).HasMaxLength(10);

            builder.HasOne(pa => pa.Patient)
                   .WithMany(p => p.Addresses)
                   .HasForeignKey(pa => pa.PatientId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
