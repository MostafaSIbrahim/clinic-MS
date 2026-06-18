
namespace SafyaClinic.Application.DTOs.Nutrition
{
    public class NutritionPackageDto
    {
        public int Id { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DurationWeeks { get; set; }
        public int SessionsPerWeek { get; set; }
        public decimal BasePrice { get; set; }
        public decimal MaxDiscountPercent { get; set; }
        public bool IsActive { get; set; }
        public List<PackageItemDto> Items { get; set; } = new List<PackageItemDto>();
    }

    public class PackageItemDto
    {
        public int Id { get; set; }
        public int? InjectionId { get; set; }
        public string? InjectionName { get; set; }
        public int? VitaminId { get; set; }
        public string? VitaminName { get; set; }
        public decimal Quantity { get; set; }
        public string? Unit { get; set; }
        public int WeekNumber { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateNutritionPackageDto
    {
        public string PackageName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal BasePrice { get; set; }
        public decimal MaxDiscountPercent { get; set; } = 20.00m;
        public List<CreatePackageItemDto> Items { get; set; } = new List<CreatePackageItemDto>();
    }

    public class CreatePackageItemDto
    {
        public int? InjectionId { get; set; }
        public int? VitaminId { get; set; }
        public decimal Quantity { get; set; } = 1.00m;
        public string? Unit { get; set; }
        public int WeekNumber { get; set; }
        public string? Notes { get; set; }
    }
}
