using Trading.Shared.Events;

namespace Trading.Core.Interfaces;

public interface IWebSocketClient
{
    Task ConnectAsync();

    Task DisconnectAsync();

    Task SubscribeCandlesAsync(
        string symbol,
        string timeframe);

    Task UnsubscribeCandlesAsync();

    event Action<CandleUpdateDto>? CandleReceived;

    event Action<TickUpdateDto>? TickReceived;

    event Action<ConnectionStatusDto>? ConnectionChanged;
}