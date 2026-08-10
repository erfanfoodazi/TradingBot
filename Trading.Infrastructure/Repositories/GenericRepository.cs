using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Trading.Core.Interfaces;

namespace Trading.Infrastructure.Repositories;

/// <summary>
/// A straightforward generic repository over EF Core. Concrete queries that
/// need eager loading or projection live in dedicated repositories.
/// </summary>
public class GenericRepository<T> : IRepository<T>
    where T : class
{
    private readonly DbContext _context;
    private readonly DbSet<T> _set;

    public GenericRepository(DbContext context)
    {
        _context = context;
        _set = context.Set<T>();
    }

    public async Task<IReadOnlyList<T>> GetAllAsync()
        => await _set.AsNoTracking().ToListAsync();

    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate)
        => await _set.AsNoTracking().Where(predicate).ToListAsync();

    public async Task<T?> GetByIdAsync(object id)
        => await _set.FindAsync(id);

    public async Task AddAsync(T entity)
        => await _set.AddAsync(entity);

    public void Update(T entity)
    {
        _set.Update(entity);
    }

    public void Remove(T entity)
        => _set.Remove(entity);
}