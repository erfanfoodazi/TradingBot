using Microsoft.EntityFrameworkCore;
using Trading.Core.Interfaces;
using Trading.Infrastructure.Database;
using Trading.Infrastructure.Database.Entities;
using Trading.Shared.Requests;
using Trading.Shared.Responses;

namespace Trading.Infrastructure.Services;

public sealed class WatchlistService : IWatchlistService
{
    private readonly IDbContextFactory<TradingDbContext> _dbContextFactory;

    public WatchlistService(IDbContextFactory<TradingDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<WatchlistResponseDto>> GetAllAsync()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var entities = await context.Watchlists
            .AsNoTracking()
            .OrderBy(w => w.Name)
            .ToListAsync();

        var result = new List<WatchlistResponseDto>();
        foreach (var entity in entities)
        {
            var symbols = await context.WatchlistSymbols
                .AsNoTracking()
                .Where(s => s.WatchlistId == entity.Id)
                .OrderBy(s => s.SortOrder)
                .Select(s => s.Symbol)
                .ToListAsync();

            result.Add(new WatchlistResponseDto
            {
                Id = entity.Id,
                Name = entity.Name,
                IsActive = entity.IsActive,
                Symbols = symbols,
            });
        }

        return result;
    }

    public async Task<WatchlistResponseDto> SaveAsync(WatchlistRequestDto request)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        WatchlistEntity entity;
        if (request.Id is int id)
        {
            entity = await context.Watchlists.FirstOrDefaultAsync(w => w.Id == id)
                     ?? throw new KeyNotFoundException($"Watchlist {id} not found.");
            entity.Name = request.Name;
            entity.IsActive = request.IsActive;

            var existing = await context.WatchlistSymbols
                .Where(s => s.WatchlistId == entity.Id)
                .ToListAsync();
            context.WatchlistSymbols.RemoveRange(existing);
        }
        else
        {
            entity = new WatchlistEntity
            {
                Name = request.Name,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
            };
            context.Watchlists.Add(entity);
        }

        var symbols = request.Symbols
            .Select((s, i) => new WatchlistSymbolEntity
            {
                WatchlistId = entity.Id,
                Symbol = s.Trim().ToUpperInvariant(),
                SortOrder = i,
            })
            .ToList();
        context.WatchlistSymbols.AddRange(symbols);

        await context.SaveChangesAsync();

        return new WatchlistResponseDto
        {
            Id = entity.Id,
            Name = entity.Name,
            IsActive = entity.IsActive,
            Symbols = symbols.Select(s => s.Symbol).ToList(),
        };
    }

    public async Task DeleteAsync(int id)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var entity = await context.Watchlists.FirstOrDefaultAsync(w => w.Id == id);
        if (entity is not null)
        {
            context.Watchlists.Remove(entity);
            await context.SaveChangesAsync();
        }
    }
}
