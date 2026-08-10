using Microsoft.EntityFrameworkCore;
using Trading.Core.Interfaces;
using Trading.Infrastructure.Database;
using Trading.Infrastructure.Database.Entities;
using Trading.Shared.Requests;
using Trading.Shared.Responses;

namespace Trading.Infrastructure.Services;

public sealed class StrategyService : IStrategyService
{
    private readonly IDbContextFactory<TradingDbContext> _dbContextFactory;

    public StrategyService(IDbContextFactory<TradingDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<StrategyResponseDto>> GetAllAsync()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.Strategies
            .AsNoTracking()
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .Select(s => new StrategyResponseDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                ParametersJson = s.ParametersJson,
                IsActive = s.IsActive,
            })
            .ToListAsync();
    }

    public async Task<StrategyResponseDto> SaveAsync(StrategyRequestDto request)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        StrategyEntity entity;
        if (request.Id is int id)
        {
            entity = await context.Strategies.FirstOrDefaultAsync(s => s.Id == id)
                     ?? throw new KeyNotFoundException($"Strategy {id} not found.");
            entity.Name = request.Name;
            entity.Description = request.Description;
            entity.ParametersJson = request.ParametersJson;
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            entity = new StrategyEntity
            {
                Name = request.Name,
                Description = request.Description,
                ParametersJson = request.ParametersJson,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow,
            };
            context.Strategies.Add(entity);
        }

        await context.SaveChangesAsync();

        return new StrategyResponseDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            ParametersJson = entity.ParametersJson,
            IsActive = entity.IsActive,
        };
    }

    public async Task DeleteAsync(int id)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var entity = await context.Strategies.FirstOrDefaultAsync(s => s.Id == id);
        if (entity is not null)
        {
            context.Strategies.Remove(entity);
            await context.SaveChangesAsync();
        }
    }
}
