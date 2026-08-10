using Trading.Shared.Responses;

namespace Trading.Core.Interfaces;

/// <summary>
/// Persists application operations and errors to SQL Server.
/// </summary>
public interface IAppLogger
{
    Task LogOperationAsync(
        string component,
        string action,
        string message,
        long? login = null);

    Task LogErrorAsync(
        string component,
        string action,
        string message,
        string? stackTrace = null,
        long? login = null);
}