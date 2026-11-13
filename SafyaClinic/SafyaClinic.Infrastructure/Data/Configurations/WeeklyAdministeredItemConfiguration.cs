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
            // M4d: each row names exactly one item (existing rows all name a package item).
            builder.ToTable("WeeklyAdministeredItems", table => table.HasCheckConstraint("CK_WeeklyAdministeredItems_OneItem",
                "(CASE WHEN [PackageItemId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [InjectionId] IS NULL THEN 0 ELSE 1 END" +
                " + CASE WHEN [VitaminId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [CustomName] IS NULL THEN 0 ELSE 1 END) = 1"));
            builder.HasKey(w => w.Id);

            builder.Property(w => w.ActualQuantity)
                   .HasPrecision(10, 2)
                   .IsRequired();

            builder.Property(w => w.AdministeredAt)
                   .HasDefaultValueSql("GETDATE()");

            builder.Property(w => w.Notes)
                   .HasMaxLength(500);

            builder.Property(w => w.CustomName).HasMaxLength(200);   // M4d
            builder.Property(w => w.Unit).HasMaxLength(50);          // M4d

            // FollowUpId → WeeklyFollowUps (CASCADE - keep this)
            builder.HasOne(w => w.FollowUp)
                   .WithMany(f => f.AdministeredItems)
                   .HasForeignKey(w => w.FollowUpId)
                   .OnDelete(DeleteBehavior.NoAction);

            // PackageItemId → PackageItems (RESTRICT - avoid multiple cascade paths)
            builder.HasOne(w => w.PackageItem)
                   .WithMany(p => p.AdministeredItems)
                   .HasForeignKey(w => w.PackageItemId)
                   .IsRequired(false)   // M4d: items outside the package have none
                   .OnDelete(DeleteBehavior.NoAction);

            // M4d: items given from the catalogues, outside the package
            builder.HasOne(w => w.Injection)
                   .WithMany()
                   .HasForeignKey(w => w.InjectionId)
                   .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(w => w.Vitamin)
                   .WithMany()
                   .HasForeignKey(w => w.VitaminId)
                   .OnDelete(DeleteBehavior.Restrict);

            // AdministeredBy → Users (RESTRICT - avoid multiple cascade paths)
            builder.HasOne(w => w.AdministerByUser)
                   .WithMany()
                   .HasForeignKey("AdministeredBy")      // ← Explicit FK column name
                   .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
