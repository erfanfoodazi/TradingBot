using Microsoft.EntityFrameworkCore;
using Trading.Infrastructure.Database.Configurations;
using Trading.Infrastructure.Database.Entities;

namespace Trading.Infrastructure.Database;

/// <summary>
/// The application's SQL Server context. All persistence concerns live here.
/// The DbContext requires a configured SqlServer connection string (see
/// "ConnectionStrings:Default" in appsettings.json).
/// </summary>
public sealed class TradingDbContext : DbContext
{
    public TradingDbContext(DbContextOptions<TradingDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppLog> Logs => Set<AppLog>();

    public DbSet<SettingEntity> Settings => Set<SettingEntity>();

    public DbSet<StrategyEntity> Strategies => Set<StrategyEntity>();

    public DbSet<WatchlistEntity> Watchlists => Set<WatchlistEntity>();

    public DbSet<WatchlistSymbolEntity> WatchlistSymbols => Set<WatchlistSymbolEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new AppLogConfiguration());
        modelBuilder.ApplyConfiguration(new SettingEntityConfiguration());
        modelBuilder.ApplyConfiguration(new StrategyEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WatchlistEntityConfiguration());
        modelBuilder.ApplyConfiguration(new WatchlistSymbolEntityConfiguration());
    }
}