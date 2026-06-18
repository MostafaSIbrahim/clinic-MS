using SafyaClinic.Domain.Entities.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SafyaClinic.Domain.Entities.Patient
{
    public class PatientAddress : BaseEntity
    {
        public int PatientId { get; set; }
        public string? Street { get; set; }
        public string City { get; set; } = string.Empty;
        public string? Governorate { get; set; }
        public string? PostalCode { get; set; }
        public bool IsPrimary { get; set; } = true;

        // Navigation properties
        public virtual Patient Patient { get; set; } = null!;
    
    }
}
