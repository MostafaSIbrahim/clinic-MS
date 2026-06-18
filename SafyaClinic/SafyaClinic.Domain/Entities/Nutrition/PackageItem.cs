using SafyaClinic.Domain.Entities.Common;


namespace SafyaClinic.Domain.Entities.Nutrition
{
    public class PackageItem:BaseEntity
    {
        public int PackageId { get; set; }
        public int? InjectionId { get; set; }
        public int? VitaminId { get; set; }
        public decimal Quantity { get; set; } = 1.00m;
        public string? Unit { get; set; }                           // Override default unit
        public int WeekNumber { get; set; }                         // 1-4 for weekly scheduling
        public string? Notes { get; set; }

        // Navigation properties
        public virtual NutritionPackage Package { get; set; } = null!;
        public virtual InjectionType? Injection { get; set; }
        public virtual VitaminType? Vitamin { get; set; }
        public virtual ICollection<WeeklyAdministeredItem> AdministeredItems { get; set; } = new List<WeeklyAdministeredItem>();
    }
}
