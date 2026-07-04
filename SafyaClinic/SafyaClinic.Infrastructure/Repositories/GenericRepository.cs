using Microsoft.EntityFrameworkCore;
using SafyaClinic.Domain.Entities.Common;
using SafyaClinic.Domain.Interfaces.Repositories;
using SafyaClinic.Infrastructure.Data;
using System.Linq.Expressions;

namespace SafyaClinic.Infrastructure.Repositories;

public class GenericRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly SafyaDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public GenericRepository(SafyaDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    // ── Queries ───────────────────────────────────────────────

    public virtual async Task<T?> GetByIdAsync(int id)
        => await _dbSet.FindAsync(id);

    public virtual async Task<IEnumerable<T>> GetAllAsync()
        => await _dbSet.AsNoTracking().ToListAsync();

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        => await _dbSet.AsNoTracking().Where(predicate).ToListAsync();

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        => await _dbSet.AsNoTracking().FirstOrDefaultAsync(predicate);

    public virtual async Task<bool> ExistsAsync(int id)
        => await _dbSet.AnyAsync(e => e.Id == id);

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
        => predicate is null
            ? await _dbSet.CountAsync()
            : await _dbSet.CountAsync(predicate);

    // ── Commands ──────────────────────────────────────────────

    public virtual async Task AddAsync(T entity)
        => await _dbSet.AddAsync(entity);

    public virtual async Task AddRangeAsync(IEnumerable<T> entities)
        => await _dbSet.AddRangeAsync(entities);

    public virtual void Update(T entity)
        => _dbSet.Update(entity);

    public virtual void Delete(T entity)
        => _dbSet.Remove(entity);

    public virtual void DeleteRange(IEnumerable<T> entities)
        => _dbSet.RemoveRange(entities);
}