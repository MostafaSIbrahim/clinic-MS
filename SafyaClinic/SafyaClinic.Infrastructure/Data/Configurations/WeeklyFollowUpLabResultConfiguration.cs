using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Nutrition;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class WeeklyFollowUpLabResultConfiguration : IEntityTypeConfiguration<WeeklyFollowUpLabResult>
    {
        public void Configure(EntityTypeBuilder<WeeklyFollowUpLabResult> builder)
        {
            // F5a: an analysis type used in recorded results cannot be deleted.
            builder.HasOne(r => r.AnalysisType)
                   .WithMany()
                   .HasForeignKey(r => r.AnalysisTypeId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}