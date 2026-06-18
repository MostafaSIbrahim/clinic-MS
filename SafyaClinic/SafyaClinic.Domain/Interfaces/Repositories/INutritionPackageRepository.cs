

namespace SafyaClinic.Domain.Interfaces.Repositories
{
    public interface INutritionPackageRepository : IRepository<Entities.Nutrition.NutritionPackage>
    {
        Task<IEnumerable<Entities.Nutrition.NutritionPackage>> GetActivePackagesAsync();
        Task<Entities.Nutrition.NutritionPackage?> GetPackageWithItemsAsync(int packageId);
    }
}
