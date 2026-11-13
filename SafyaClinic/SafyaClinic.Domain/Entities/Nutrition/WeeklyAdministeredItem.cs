using SafyaClinic.Domain.Entities.Common;
using SafyaClinic.Domain.Identity;

namespace SafyaClinic.Domain.Entities.Nutrition
{
    public class WeeklyAdministeredItem:BaseEntity
    {
        public int FollowUpId { get; set; }
        // M4d: exactly one of a package item, an injection type, a vitamin type or a typed drug name.
        public int? PackageItemId { get; set; }
        public int? InjectionId { get; set; }
        public int? VitaminId { get; set; }
        public string? CustomName { get; set; }
        public string? Unit { get; set; }
        public decimal ActualQuantity { get; set; }
        public int AdministeredBy { get; set; }
        public DateTime AdministeredAt { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }

        // Navigation properties
        public virtual WeeklyFollowUp FollowUp { get; set; } = null!;
        public virtual PackageItem? PackageItem { get; set; }
        public virtual InjectionType? Injection { get; set; }
        public virtual VitaminType? Vitamin { get; set; }
        public virtual User AdministerByUser { get; set; } = null!;
    }
}
