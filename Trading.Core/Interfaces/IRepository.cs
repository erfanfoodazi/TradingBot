using System.Linq.Expressions;

namespace Trading.Core.Interfaces;

/// <summary>
/// Generic repository abstraction (Clean Architecture). Keeps the domain
/// decoupled from the concrete ORM (EF Core) so the data layer is swappable.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<IReadOnlyList<T>> GetAllAsync();

    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate);

    Task<T?> GetByIdAsync(object id);

    Task AddAsync(T entity);

    void Update(T entity);

    void Remove(T entity);
}