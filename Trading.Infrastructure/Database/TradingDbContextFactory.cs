using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Trading.Infrastructure.Database;

/// <summary>
/// Used only by the EF Core tools (`dotnet ef migrations add` /
/// <c>Database.Update()</c>). At runtime the context is registered with a
/// connection string from appsettings.json.
/// </summary>
public sealed class TradingDbContextFactory : IDesignTimeDbContextFactory<TradingDbContext>
{
    public TradingDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__DEFAULT")
            ?? "Server=localhost;Database=TradingBot;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<TradingDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new TradingDbContext(options);
    }
}