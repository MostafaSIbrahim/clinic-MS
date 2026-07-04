using Microsoft.EntityFrameworkCore;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Interfaces.Repositories;
using SafyaClinic.Infrastructure.Data;


 
namespace SafyaClinic.Infrastructure.Repositories;

public class NutritionPackageRepository
    : GenericRepository<NutritionPackage>, INutritionPackageRepository
{
    public NutritionPackageRepository(SafyaDbContext context) : base(context) { }

    public async Task<IEnumerable<NutritionPackage>> GetActivePackagesAsync()
        => await _dbSet
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Include(p => p.Items)
                .ThenInclude(i => i.Injection)
            .Include(p => p.Items)
                .ThenInclude(i => i.Vitamin)
            .OrderBy(p => p.PackageName)
            .ToListAsync();

    public async Task<NutritionPackage?> GetPackageWithItemsAsync(int packageId)
        => await _dbSet
            .AsNoTracking()
            .Include(p => p.Items)
                .ThenInclude(i => i.Injection)
            .Include(p => p.Items)
                .ThenInclude(i => i.Vitamin)
            .FirstOrDefaultAsync(p => p.Id == packageId);
}
