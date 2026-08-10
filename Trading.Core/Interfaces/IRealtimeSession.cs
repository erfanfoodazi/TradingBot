namespace Trading.Core.Interfaces;

/// <summary>
/// Holds the symbol/timeframe the UI currently wants to react to. Shared between
/// the UI (MainViewModel) and the background connection worker so the worker can
/// (re)establish the tick/candle channels even if the app recovered late.
/// </summary>
public interface IRealtimeSession
{
    string Symbol { get; set; }

    string Timeframe { get; set; }
}