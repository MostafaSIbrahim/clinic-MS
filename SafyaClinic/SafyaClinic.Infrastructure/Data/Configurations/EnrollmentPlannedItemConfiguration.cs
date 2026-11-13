using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Nutrition;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    /// <summary>M4d: a patient's plan for the coming weeks of an enrollment.</summary>
    public class EnrollmentPlannedItemConfiguration : IEntityTypeConfiguration<EnrollmentPlannedItem>
    {
        public void Configure(EntityTypeBuilder<EnrollmentPlannedItem> builder)
        {
            builder.ToTable("EnrollmentPlannedItems", table =>
            {
                table.HasCheckConstraint("CK_EnrollmentPlannedItems_OneItem",
                    "(CASE WHEN [PackageItemId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [InjectionId] IS NULL THEN 0 ELSE 1 END" +
                    " + CASE WHEN [VitaminId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [CustomName] IS NULL THEN 0 ELSE 1 END) = 1");
                table.HasCheckConstraint("CK_EnrollmentPlannedItems_Week", "[WeekNumber] BETWEEN 1 AND 4");
                table.HasCheckConstraint("CK_EnrollmentPlannedItems_Quantity", "[Quantity] > 0");
            });
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Quantity).HasPrecision(10, 2);
            builder.Property(p => p.CustomName).HasMaxLength(200);
            builder.Property(p => p.Unit).HasMaxLength(50);
            builder.Property(p => p.Notes).HasMaxLength(500);
            builder.HasIndex(p => new { p.EnrollmentId, p.WeekNumber });

            // History is never deleted by cascade.
            builder.HasOne(p => p.Enrollment).WithMany(e => e.PlannedItems)
                   .HasForeignKey(p => p.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(p => p.PackageItem).WithMany()
                   .HasForeignKey(p => p.PackageItemId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(p => p.Injection).WithMany()
                   .HasForeignKey(p => p.InjectionId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(p => p.Vitamin).WithMany()
                   .HasForeignKey(p => p.VitaminId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
