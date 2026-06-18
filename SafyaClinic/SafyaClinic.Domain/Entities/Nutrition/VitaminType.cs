
using SafyaClinic.Domain.Entities.Common;

namespace SafyaClinic.Domain.Entities.Nutrition
{
    public class VitaminType: BaseEntity
    {
        public string VitaminName { get; set; } = string.Empty;
        public string? Formulation { get; set; }                    // Injectable, Oral, IV
        public string Unit { get; set; } = string.Empty;
        public string? Description { get; set; };
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<PackageItem> PackageItems { get; set; } = new List<PackageItem>();
    }
}
