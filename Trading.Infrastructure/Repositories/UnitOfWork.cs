using Microsoft.EntityFrameworkCore;
using Trading.Core.Interfaces;

namespace Trading.Infrastructure.Repositories;

/// <summary>
/// Coordinates changes across repositories and commits them in a single
/// transaction via SaveChanges. Services depend on this abstraction instead of
/// the DbContext, keeping the domain decoupled from EF Core.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly DbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();

    public UnitOfWork(DbContext context)
    {
        _context = context;
    }

    public IRepository<T> Repository<T>() where T : class
    {
        if (_repositories.TryGetValue(typeof(T), out var existing))
            return (IRepository<T>)existing;

        var repository = new GenericRepository<T>(_context);
        _repositories[typeof(T)] = repository;
        return repository;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}