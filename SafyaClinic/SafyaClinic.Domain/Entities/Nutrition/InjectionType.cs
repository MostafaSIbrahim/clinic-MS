using SafyaClinic.Domain.Entities.Common;


namespace SafyaClinic.Domain.Entities.Nutrition
{
    public class InjectionType: BaseEntity
    {
        public string InjectionName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;           // mg, ml, IU, units
        public string? Description { get; set; }
        public string? DefaultDosage { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<PackageItem> PackageItems { get; set; } = new List<PackageItem>();
    }
}
