using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Analysis;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class AnalysisTypeConfiguration : IEntityTypeConfiguration<AnalysisType>
    {
        public void Configure(EntityTypeBuilder<AnalysisType> builder)
        {
            builder.ToTable("AnalysisTypes");
            builder.HasKey(at => at.Id);
            builder.Property(at => at.TypeName).IsRequired().HasMaxLength(100);
            builder.Property(at => at.Description).HasMaxLength(500);
            builder.Property(at => at.DefaultCost).HasPrecision(18, 2);
            builder.Property(at => at.PreparationInstructions).HasMaxLength(1000);
        }
    }
}
