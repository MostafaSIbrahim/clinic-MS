using SafyaClinic.Domain.Entities.Common;
using SafyaClinic.Domain.Identity;

namespace SafyaClinic.Domain.Entities.Nutrition
{
    public class WeeklyAdministeredItem:BaseEntity
    {
        public int FollowUpId { get; set; }
        public int PackageItemId { get; set; }
        public decimal ActualQuantity { get; set; }
        public int AdministeredBy { get; set; }
        public DateTime AdministeredAt { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }

        // Navigation properties
        public virtual WeeklyFollowUp FollowUp { get; set; } = null!;
        public virtual PackageItem PackageItem { get; set; } = null!;
        public virtual User AdministerByUser { get; set; } = null!;
    }
}
