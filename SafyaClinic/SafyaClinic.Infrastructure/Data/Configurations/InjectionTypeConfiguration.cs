using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Nutrition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class InjectionTypeConfiguration : IEntityTypeConfiguration<InjectionType>
    {
        public void Configure(EntityTypeBuilder<InjectionType> builder)
        {
            builder.ToTable("InjectionTypes");
            builder.HasKey(it => it.Id);
            builder.Property(it => it.InjectionName).IsRequired().HasMaxLength(100);
            builder.Property(it => it.Unit).IsRequired().HasMaxLength(50);
            builder.Property(it => it.Description).HasMaxLength(500);
            builder.Property(it => it.DefaultDosage).HasMaxLength(100);
        }
    }
}
