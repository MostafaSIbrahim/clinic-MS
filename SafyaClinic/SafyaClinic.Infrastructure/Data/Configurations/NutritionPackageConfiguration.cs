using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Nutrition;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class NutritionPackageConfiguration : IEntityTypeConfiguration<NutritionPackage>
    {
        public void Configure(EntityTypeBuilder<NutritionPackage> builder)
        {
            builder.ToTable("NutritionPackages");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.PackageName).IsRequired().HasMaxLength(100);
            builder.Property(p => p.Description).HasMaxLength(500);
            builder.Property(p => p.BasePrice).HasPrecision(18, 2);
            builder.Property(p => p.MaxDiscountPercent).HasPrecision(5, 2);
            builder.HasIndex(p => p.IsActive);
        }
    }
}
