using Trading.Core.Interfaces;

namespace Trading.Infrastructure.Services;

public sealed class RealtimeSession : IRealtimeSession
{
    public string Symbol { get; set; } = string.Empty;

    public string Timeframe { get; set; } = "M1";
}