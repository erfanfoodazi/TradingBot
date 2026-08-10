using Microsoft.EntityFrameworkCore;
using Trading.Core.Interfaces;
using Trading.Infrastructure.Database;
using Trading.Infrastructure.Database.Entities;
using Trading.Shared.Requests;
using Trading.Shared.Responses;

namespace Trading.Infrastructure.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly IDbContextFactory<TradingDbContext> _dbContextFactory;

    public SettingsService(IDbContextFactory<TradingDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<SettingResponseDto>> GetAllAsync()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.Settings
            .AsNoTracking()
            .OrderBy(s => s.Key)
            .Select(s => new SettingResponseDto { Key = s.Key, Value = s.Value })
            .ToListAsync();
    }

    public async Task<string?> GetAsync(string key)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return (await context.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key))?.Value;
    }

    public async Task SetAsync(SettingRequestDto request)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var existing = await context.Settings.FirstOrDefaultAsync(s => s.Key == request.Key);
        if (existing is null)
        {
            context.Settings.Add(new SettingEntity
            {
                Key = request.Key,
                Value = request.Value,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Value = request.Value;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }
}
