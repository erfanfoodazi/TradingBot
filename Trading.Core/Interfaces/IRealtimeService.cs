using Trading.Shared.Events;

namespace Trading.Core.Interfaces;

/// <summary>
/// Orchestrates real-time streams (ticks, candles, positions, account)
/// supplied by the Python WebSocket backend. Raises typed events that the
/// presentation layer (ViewModels) subscribes to.
/// </summary>
public interface IRealtimeService
{
    bool IsConnected { get; }

    Task ConnectAsync();

    Task DisconnectAsync();

    Task StartTicksAsync(string symbol);

    Task StopTicksAsync();

    Task StartCandlesAsync(string symbol, string timeframe);

    Task StopCandlesAsync();

    Task StartPositionsAsync();

    Task StopPositionsAsync();

    Task StartAccountAsync();

    Task StopAccountAsync();

    Task StopAllAsync();

    event Action<TickUpdateDto>? TickReceived;

    event Action<CandleUpdateDto>? CandleReceived;

    event Action<List<PositionUpdateDto>>? PositionsReceived;

    event Action<AccountUpdateDto>? AccountReceived;

    event Action<ConnectionStatusDto>? ConnectionChanged;
}