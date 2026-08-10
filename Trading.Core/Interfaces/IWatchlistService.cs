using Trading.Shared.Requests;
using Trading.Shared.Responses;

namespace Trading.Core.Interfaces;

/// <summary>
/// Manages persistent user-defined watchlists backed by SQL Server.
/// </summary>
public interface IWatchlistService
{
    Task<List<WatchlistResponseDto>> GetAllAsync();

    Task<WatchlistResponseDto> SaveAsync(WatchlistRequestDto request);

    Task DeleteAsync(int id);
}