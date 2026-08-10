using Microsoft.EntityFrameworkCore;
using Trading.Core.Interfaces;
using Trading.Infrastructure.Database;
using Trading.Infrastructure.Database.Entities;

namespace Trading.Infrastructure.Services;

/// <summary>
/// Appends operations and errors to SQL Server. Uses a short-lived DbContext
/// per write so subscribers can keep this logger as a singleton.
/// </summary>
public class AppSqlLogger : IAppLogger
{
    private readonly IDbContextFactory<TradingDbContext> _dbContextFactory;

    public AppSqlLogger(IDbContextFactory<TradingDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task LogOperationAsync(
        string component, string action, string message, long? login = null)
    {
        await LogAsync("Information", component, action, message, null, login);
    }

    public async Task LogErrorAsync(
        string component, string action, string message, string? stackTrace = null, long? login = null)
    {
        await LogAsync("Error", component, action, message, stackTrace, login);
    }

    private async Task LogAsync(
        string level, string component, string action, string message, string? stackTrace, long? login)
    {
        try
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            context.Logs.Add(new AppLog
            {
                Level = level,
                Component = component,
                Action = action,
                Message = message,
                StackTrace = stackTrace,
                Login = login,
                CreatedAt = DateTime.UtcNow,
            });
            await context.SaveChangesAsync();
        }
        catch
        {
            // Logging must never break the caller.
        }
    }
}
