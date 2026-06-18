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
    public class PatientPhoneConfiguration : IEntityTypeConfiguration<PatientPhone>
    {
        public void Configure(EntityTypeBuilder<PatientPhone> builder)
        {
            builder.ToTable("PatientPhones");
            builder.HasKey(pp => pp.Id);
            builder.Property(pp => pp.PhoneNumber).IsRequired().HasMaxLength(20);
            builder.Property(pp => pp.PhoneType).HasMaxLength(20);

            builder.HasOne(pp => pp.Patient)
                   .WithMany(p => p.Phones)
                   .HasForeignKey(pp => pp.PatientId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
