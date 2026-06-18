using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SafyaClinic.Domain.Entities.Common
{
    public class AuditableEntity: BaseEntity
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int  CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
