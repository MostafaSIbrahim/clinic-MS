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
    internal class VitaminTypeConfiguration : IEntityTypeConfiguration<VitaminType>
    {
        public void Configure(EntityTypeBuilder<VitaminType> builder)
        {
            throw new NotImplementedException();
        }
    }
}
