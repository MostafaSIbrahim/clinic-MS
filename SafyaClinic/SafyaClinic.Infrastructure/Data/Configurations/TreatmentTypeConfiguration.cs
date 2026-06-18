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
    public class TreatmentTypeConfiguration : IEntityTypeConfiguration<TreatmentType>
    {
        public void Configure(EntityTypeBuilder<TreatmentType> builder)
        {
            builder.ToTable("TreatmentTypes");
            builder.HasKey(tt => tt.Id);
            builder.Property(tt => tt.TypeName).IsRequired().HasMaxLength(100);
            builder.Property(tt => tt.Description).HasMaxLength(500);
            builder.Property(tt => tt.DefaultCost).HasPrecision(18, 2);
            builder.Property(tt => tt.Category).HasConversion<string>().HasMaxLength(20);
        }
    }
}
