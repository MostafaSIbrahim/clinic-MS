using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Identity;


namespace SafyaClinic.Infrastructure.Data.Configurations
{
    internal class WeeklyAdministeredItemConfiguration : IEntityTypeConfiguration<WeeklyAdministeredItem>
    {
        public void Configure(EntityTypeBuilder<WeeklyAdministeredItem> builder)
        {
            builder.ToTable("WeeklyAdministeredItems");
            builder.HasKey(w => w.Id);

            builder.Property(w => w.ActualQuantity)
                   .HasPrecision(10, 2)
                   .IsRequired();

            builder.Property(w => w.AdministeredAt)
                   .HasDefaultValueSql("GETDATE()");

            builder.Property(w => w.Notes)
                   .HasMaxLength(500);

            // FollowUpId → WeeklyFollowUps (CASCADE - keep this)
            builder.HasOne(w => w.FollowUp)
                   .WithMany(f => f.AdministeredItems)
                   .HasForeignKey(w => w.FollowUpId)
                   .OnDelete(DeleteBehavior.NoAction);

            // PackageItemId → PackageItems (RESTRICT - avoid multiple cascade paths)
            builder.HasOne(w => w.PackageItem)
                   .WithMany()
                   .HasForeignKey(w => w.PackageItemId)
                   .OnDelete(DeleteBehavior.NoAction);

            // AdministeredBy → Users (RESTRICT - avoid multiple cascade paths)
            builder.HasOne(w => w.AdministerByUser)
                   .WithMany()
                   .HasForeignKey("AdministeredBy")      // ← Explicit FK column name
                   .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
