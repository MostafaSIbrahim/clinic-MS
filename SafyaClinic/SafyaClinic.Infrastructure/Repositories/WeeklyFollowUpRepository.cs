using Microsoft.EntityFrameworkCore;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Interfaces.Repositories;
using SafyaClinic.Infrastructure.Data;

namespace SafyaClinic.Infrastructure.Repositories;

public class WeeklyFollowUpRepository
    : GenericRepository<WeeklyFollowUp>, IWeeklyFollowUpRepository
{
    public WeeklyFollowUpRepository(SafyaDbContext context) : base(context) { }

    public async Task<IEnumerable<WeeklyFollowUp>> GetByEnrollmentAsync(int enrollmentId)
        => await _dbSet
            .AsNoTracking()
            .Where(f => f.EnrollmentId == enrollmentId)
            .Include(f => f.AdministeredItems)
                .ThenInclude(a => a.PackageItem)
                    .ThenInclude(pi => pi.Injection)
            .Include(f => f.AdministeredItems)
                .ThenInclude(a => a.PackageItem)
                    .ThenInclude(pi => pi.Vitamin)
            .Include(f => f.LabResults)
                .ThenInclude(l => l.AnalysisType)
            .OrderBy(f => f.WeekNumber)
            .ToListAsync();

    public async Task<WeeklyFollowUp?> GetFollowUpWithDetailsAsync(int followUpId)
        => await _dbSet
            .AsNoTracking()
            .Include(f => f.Enrollment)
                .ThenInclude(e => e.Patient)
            .Include(f => f.Enrollment)
                .ThenInclude(e => e.Package)
            .Include(f => f.AdministeredItems)
                .ThenInclude(a => a.PackageItem)
                    .ThenInclude(pi => pi.Injection)
            .Include(f => f.AdministeredItems)
                .ThenInclude(a => a.PackageItem)
                    .ThenInclude(pi => pi.Vitamin)
            .Include(f => f.AdministeredItems)
                .ThenInclude(a => a.AdministerByUser)
            .Include(f => f.LabResults)
                .ThenInclude(l => l.AnalysisType)
            .FirstOrDefaultAsync(f => f.Id == followUpId);
}