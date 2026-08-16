namespace Trading.Shared.Responses;

/// <summary>
/// Marker drawn on the chart as an overlay for a finished backtest.
/// </summary>
public enum BackTestMarkerKind
{
    Entry,
    StopLoss,
    TakeProfit
}

/// <summary>
/// A single marker (entry / SL / TP) positioned in time-price space.
/// </summary>
public sealed class BackTestMarker
{
    public BackTestMarkerKind Kind { get; init; }

    public DateTime Time { get; init; }

    public double Price { get; init; }
}