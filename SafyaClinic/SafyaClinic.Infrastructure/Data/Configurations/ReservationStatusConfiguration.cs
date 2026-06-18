using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafyaClinic.Domain.Entities.Reservation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SafyaClinic.Infrastructure.Data.Configurations
{
    public class ReservationStatusConfiguration : IEntityTypeConfiguration<ReservationStatus>
    {
        public void Configure(EntityTypeBuilder<ReservationStatus> builder)
        {
            builder.ToTable("ReservationStatuses");
            builder.HasKey(rs => rs.Id);
            builder.Property(rs => rs.StatusName).IsRequired().HasMaxLength(50);
            builder.Property(rs => rs.Description).HasMaxLength(255);
            builder.Property(rs => rs.ColorCode).HasMaxLength(7);
            builder.HasIndex(rs => rs.StatusName).IsUnique();
        }
    }
}
