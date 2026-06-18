using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Nutrition;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class PackageItemConfiguration : IEntityTypeConfiguration<PackageItem>
    {
        public void Configure(EntityTypeBuilder<PackageItem> builder)
        {
            builder.ToTable("PackageItems");
            builder.HasKey(pi => pi.Id);
            builder.Property(pi => pi.Quantity).HasPrecision(10, 2);
            builder.Property(pi => pi.Unit).HasMaxLength(50);
            builder.Property(pi => pi.Notes).HasMaxLength(255);
            builder.HasOne(pi => pi.Package)
                   .WithMany(p => p.Items)
                   .HasForeignKey(pi => pi.PackageId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
