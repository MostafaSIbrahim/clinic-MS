using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Nutrition;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    internal class VitaminTypeConfiguration : IEntityTypeConfiguration<VitaminType>
    {
        public void Configure(EntityTypeBuilder<VitaminType> builder)
        {
            builder.ToTable("VitaminTypes");
            builder.HasKey(v => v.Id);

            builder.Property(v => v.VitaminName)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(v => v.Formulation)
                   .HasMaxLength(100);

            builder.Property(v => v.Unit)
                   .IsRequired()
                   .HasMaxLength(50);

            builder.Property(v => v.Description)
                   .HasMaxLength(500);

            builder.Property(v => v.IsActive)
                   .HasDefaultValue(true);
        }
    }
}