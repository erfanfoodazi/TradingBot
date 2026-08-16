namespace Trading.Core.Indicators;

/// <summary>
/// Technical indicators that can be overlaid on the chart. The enumeration
/// drives which computations are performed by <see cref="TechnicalIndicators"/>
/// and how the results are rendered by the UI chart.
/// </summary>
public enum IndicatorType
{
    /// <summary>Simple Moving Average of Close (price overlay).</summary>
    Sma,

    /// <summary>Exponential Moving Average of Close (price overlay).</summary>
    Ema,

    /// <summary>Bollinger Bands (price overlay).</summary>
    BollingerBands,

    /// <summary>Volume Weighted Average Price (price overlay).</summary>
    Vwap,

    /// <summary>Average True Range (price overlay).</summary>
    Atr,

    /// <summary>Fibonacci retracement levels (price overlay).</summary>
    Fibonacci,

    /// <summary>Ichimoku Kinko Hyo cloud (price overlay).</summary>
    Ichimoku,

    /// <summary>Relative Strength Index (oscillator panel).</summary>
    Rsi,

    /// <summary>Stochastic Oscillator (oscillator panel).</summary>
    Stochastic,

    /// <summary>MACD (oscillator panel).</summary>
    Macd,
}