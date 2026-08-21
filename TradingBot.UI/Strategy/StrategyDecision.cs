namespace TradingBot.UI.Strategy;

/// <summary>
/// A single decision the <see cref="ManualStrategy"/> made for one candle,
/// captured for back test review.
/// </summary>
public sealed class StrategyDecision
{
    public DateTime Time { get; init; }

    public ManualStrategy.StrategyState State { get; init; }

    public ManualStrategy.TrendMode Trend { get; init; }

    public ManualStrategy.ReversMode Reversal { get; init; }

    public int ReversalCount { get; init; }

    /// <summary>Entry price in effect when the decision was made.</summary>
    public double Entry { get; init; }

    /// <summary>Candle OHLC when the decision is tied to a specific candle.</summary>
    public double Open { get; init; }

    public double High { get; init; }

    public double Low { get; init; }

    public double Close { get; init; }

    public string Decision { get; init; } = string.Empty;
}