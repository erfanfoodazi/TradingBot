using Trading.Shared.Responses;

namespace Trading.Core.Indicators;

#region Result DTOs

public sealed class MacdResultDto
{
    public List<double?> MacdLine { get; init; } = [];
    public List<double?> SignalLine { get; init; } = [];
    public List<double?> Histogram { get; init; } = [];
}

public sealed class StochasticResultDto
{
    public List<double?> K { get; init; } = [];
    public List<double?> D { get; init; } = [];
}

public sealed class BollingerBandsResultDto
{
    public List<double?> Upper { get; init; } = [];
    public List<double?> Middle { get; init; } = [];
    public List<double?> Lower { get; init; } = [];
}

public sealed class FibonacciLevelsResultDto
{
    public double SwingHigh { get; init; }
    public double SwingLow { get; init; }

    /// <summary>Ratio -> price level (e.g. 0.618 -> 1.2345).</summary>
    public Dictionary<double, double> Levels { get; init; } = new();
}

public sealed class IchimokuResultDto
{
    public List<double?> TenkanSen { get; init; } = [];
    public List<double?> KijunSen { get; init; } = [];
    public List<double?> SenkouSpanA { get; init; } = [];
    public List<double?> SenkouSpanB { get; init; } = [];
    public List<double?> ChikouSpan { get; init; } = [];
}

#endregion

/// <summary>
/// Stateless technical indicator calculations.
///
/// All indicators take an ordered list of CLOSED candles (oldest -> newest)
/// and return values index-aligned with the input list: result[i]
/// corresponds to candles[i]. Indices where there isn't enough history yet
/// are null - always check HasValue before using a value.
///
/// None of these methods mutate the input list or hold any state between
/// calls, so a single static instance is safe to call concurrently for
/// different symbols/candle sets.
/// </summary>
public static class TechnicalIndicators
{
    #region Trend

    /// <summary>
    /// Simple Moving Average of Close.
    /// </summary>
    public static List<double?> Sma(IReadOnlyList<CandleResponseDto> candles, int period)
    {
        ValidatePeriod(period);

        var result = new List<double?>(candles.Count);

        double sum = 0;

        for (int i = 0; i < candles.Count; i++)
        {
            sum += candles[i].Close;

            if (i >= period)
                sum -= candles[i - period].Close;

            result.Add(i >= period - 1 ? sum / period : null);
        }

        return result;
    }

    /// <summary>
    /// Exponential Moving Average of Close.
    /// Seeded with a Simple Moving Average of the first `period` closes.
    /// </summary>
    public static List<double?> Ema(IReadOnlyList<CandleResponseDto> candles, int period)
    {
        ValidatePeriod(period);

        var result = new List<double?>(new double?[candles.Count]);

        if (candles.Count < period)
            return result;

        double multiplier = 2.0 / (period + 1);

        double seed = 0;
        for (int i = 0; i < period; i++)
            seed += candles[i].Close;

        seed /= period;

        result[period - 1] = seed;

        double prevEma = seed;

        for (int i = period; i < candles.Count; i++)
        {
            double ema = (candles[i].Close - prevEma) * multiplier + prevEma;
            result[i] = ema;
            prevEma = ema;
        }

        return result;
    }

    /// <summary>
    /// MACD: Fast EMA minus Slow EMA, plus a Signal line (EMA of the MACD
    /// line) and a Histogram (MACD line minus Signal line).
    /// Standard defaults: 12 / 26 / 9.
    /// </summary>
    public static MacdResultDto Macd(
        IReadOnlyList<CandleResponseDto> candles,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9)
    {
        var fastEma = Ema(candles, fastPeriod);
        var slowEma = Ema(candles, slowPeriod);

        var macdLine = new List<double?>(candles.Count);

        for (int i = 0; i < candles.Count; i++)
        {
            macdLine.Add(fastEma[i].HasValue && slowEma[i].HasValue
                ? fastEma[i]!.Value - slowEma[i]!.Value
                : null);
        }

        var signalLine = EmaOfSeries(macdLine, signalPeriod);

        var histogram = new List<double?>(candles.Count);

        for (int i = 0; i < candles.Count; i++)
        {
            histogram.Add(macdLine[i].HasValue && signalLine[i].HasValue
                ? macdLine[i]!.Value - signalLine[i]!.Value
                : null);
        }

        return new MacdResultDto
        {
            MacdLine = macdLine,
            SignalLine = signalLine,
            Histogram = histogram
        };
    }

    #endregion

    #region Momentum

    /// <summary>
    /// Relative Strength Index using Wilder's smoothing (the standard RSI).
    /// </summary>
    public static List<double?> Rsi(IReadOnlyList<CandleResponseDto> candles, int period = 14)
    {
        ValidatePeriod(period);

        var result = new List<double?>(new double?[candles.Count]);

        if (candles.Count <= period)
            return result;

        double avgGain = 0;
        double avgLoss = 0;

        for (int i = 1; i <= period; i++)
        {
            double change = candles[i].Close - candles[i - 1].Close;
            avgGain += Math.Max(change, 0);
            avgLoss += Math.Max(-change, 0);
        }

        avgGain /= period;
        avgLoss /= period;

        result[period] = CalculateRsi(avgGain, avgLoss);

        for (int i = period + 1; i < candles.Count; i++)
        {
            double change = candles[i].Close - candles[i - 1].Close;
            double gain = Math.Max(change, 0);
            double loss = Math.Max(-change, 0);

            // Wilder's smoothing (not a plain moving average).
            avgGain = (avgGain * (period - 1) + gain) / period;
            avgLoss = (avgLoss * (period - 1) + loss) / period;

            result[i] = CalculateRsi(avgGain, avgLoss);
        }

        return result;
    }

    private static double CalculateRsi(double avgGain, double avgLoss)
    {
        if (avgLoss == 0)
            return 100;

        double rs = avgGain / avgLoss;
        return 100 - (100 / (1 + rs));
    }

    /// <summary>
    /// Stochastic Oscillator.
    /// %K = raw %K smoothed over `smoothK` periods.
    /// %D = Simple Moving Average of %K over `dPeriod` periods.
    /// Standard defaults: 14 / 3 / 3.
    /// </summary>
    public static StochasticResultDto Stochastic(
        IReadOnlyList<CandleResponseDto> candles,
        int kPeriod = 14,
        int smoothK = 3,
        int dPeriod = 3)
    {
        ValidatePeriod(kPeriod);

        var rawK = new List<double?>(new double?[candles.Count]);

        for (int i = kPeriod - 1; i < candles.Count; i++)
        {
            double highestHigh = double.MinValue;
            double lowestLow = double.MaxValue;

            for (int j = i - kPeriod + 1; j <= i; j++)
            {
                highestHigh = Math.Max(highestHigh, candles[j].High);
                lowestLow = Math.Min(lowestLow, candles[j].Low);
            }

            double range = highestHigh - lowestLow;

            rawK[i] = range > 0
                ? 100 * (candles[i].Close - lowestLow) / range
                : 0;
        }

        var smoothedK = SmaOfSeries(rawK, smoothK);
        var d = SmaOfSeries(smoothedK, dPeriod);

        return new StochasticResultDto
        {
            K = smoothedK,
            D = d
        };
    }

    #endregion

    #region Volatility

    /// <summary>
    /// Bollinger Bands: Simple Moving Average middle band, +/- `stdDevMultiplier`
    /// standard deviations of Close for the upper/lower bands.
    /// Standard defaults: period 20, multiplier 2.0.
    /// </summary>
    public static BollingerBandsResultDto BollingerBands(
        IReadOnlyList<CandleResponseDto> candles,
        int period = 20,
        double stdDevMultiplier = 2.0)
    {
        ValidatePeriod(period);

        var middle = Sma(candles, period);
        var upper = new List<double?>(new double?[candles.Count]);
        var lower = new List<double?>(new double?[candles.Count]);

        for (int i = period - 1; i < candles.Count; i++)
        {
            double mean = middle[i]!.Value;

            double sumSquares = 0;
            for (int j = i - period + 1; j <= i; j++)
                sumSquares += Math.Pow(candles[j].Close - mean, 2);

            double stdDev = Math.Sqrt(sumSquares / period);

            upper[i] = mean + stdDevMultiplier * stdDev;
            lower[i] = mean - stdDevMultiplier * stdDev;
        }

        return new BollingerBandsResultDto
        {
            Middle = middle,
            Upper = upper,
            Lower = lower
        };
    }

    /// <summary>
    /// Average True Range using Wilder's smoothing.
    /// </summary>
    public static List<double?> Atr(IReadOnlyList<CandleResponseDto> candles, int period = 14)
    {
        ValidatePeriod(period);

        var result = new List<double?>(new double?[candles.Count]);

        if (candles.Count <= period)
            return result;

        var trueRanges = new double[candles.Count];

        for (int i = 1; i < candles.Count; i++)
        {
            double highLow = candles[i].High - candles[i].Low;
            double highPrevClose = Math.Abs(candles[i].High - candles[i - 1].Close);
            double lowPrevClose = Math.Abs(candles[i].Low - candles[i - 1].Close);

            trueRanges[i] = Math.Max(highLow, Math.Max(highPrevClose, lowPrevClose));
        }

        double avgTr = 0;
        for (int i = 1; i <= period; i++)
            avgTr += trueRanges[i];

        avgTr /= period;

        result[period] = avgTr;

        for (int i = period + 1; i < candles.Count; i++)
        {
            avgTr = (avgTr * (period - 1) + trueRanges[i]) / period;
            result[i] = avgTr;
        }

        return result;
    }

    #endregion

    #region Volume

    /// <summary>
    /// Volume Weighted Average Price, cumulative across the ENTIRE input
    /// list. VWAP is normally reset per session, so pass only the candles
    /// belonging to the session/window you want (e.g. today's candles) -
    /// this method does not detect session boundaries on its own.
    ///
    /// Uses TickVolume as the volume proxy, since forex/CFD symbols don't
    /// report real traded volume.
    /// </summary>
    public static List<double?> Vwap(IReadOnlyList<CandleResponseDto> candles)
    {
        var result = new List<double?>(candles.Count);

        double cumulativePv = 0;
        double cumulativeVolume = 0;

        foreach (var c in candles)
        {
            double typicalPrice = (c.High + c.Low + c.Close) / 3.0;
            double volume = c.TickVolume;

            cumulativePv += typicalPrice * volume;
            cumulativeVolume += volume;

            result.Add(cumulativeVolume > 0 ? cumulativePv / cumulativeVolume : null);
        }

        return result;
    }

    #endregion

    #region Other

    /// <summary>
    /// Fibonacci retracement levels from the highest High and lowest Low
    /// within the most recent `lookback` candles.
    /// </summary>
    public static FibonacciLevelsResultDto FibonacciRetracement(
        IReadOnlyList<CandleResponseDto> candles,
        int lookback)
    {
        if (candles.Count == 0)
            return new FibonacciLevelsResultDto();

        int start = Math.Max(0, candles.Count - lookback);

        double high = double.MinValue;
        double low = double.MaxValue;

        for (int i = start; i < candles.Count; i++)
        {
            high = Math.Max(high, candles[i].High);
            low = Math.Min(low, candles[i].Low);
        }

        double range = high - low;

        // Standard retracement ratios.
        double[] ratios = { 0.0, 0.236, 0.382, 0.5, 0.618, 0.786, 1.0 };

        var levels = new Dictionary<double, double>();

        foreach (var ratio in ratios)
            levels[ratio] = high - range * ratio;

        return new FibonacciLevelsResultDto
        {
            SwingHigh = high,
            SwingLow = low,
            Levels = levels
        };
    }

    /// <summary>
    /// Ichimoku Kinko Hyo. Standard periods: Tenkan 9, Kijun 26, Senkou B 52.
    ///
    /// NOTE ON PLOTTING: in classic Ichimoku charting, Senkou Span A/B are
    /// drawn 26 periods AHEAD of the candle that produced them, and Chikou
    /// Span is drawn 26 periods BEHIND. This method returns all series
    /// index-aligned with the input candles (no shift applied) - shift the
    /// indices yourself at render time if you want the traditional cloud
    /// display.
    /// </summary>
    public static IchimokuResultDto Ichimoku(
        IReadOnlyList<CandleResponseDto> candles,
        int tenkanPeriod = 9,
        int kijunPeriod = 26,
        int senkouBPeriod = 52)
    {
        var tenkan = MidpointSeries(candles, tenkanPeriod);
        var kijun = MidpointSeries(candles, kijunPeriod);

        var senkouA = new List<double?>(candles.Count);
        for (int i = 0; i < candles.Count; i++)
        {
            senkouA.Add(tenkan[i].HasValue && kijun[i].HasValue
                ? (tenkan[i]!.Value + kijun[i]!.Value) / 2.0
                : null);
        }

        var senkouB = MidpointSeries(candles, senkouBPeriod);

        var chikou = new List<double?>(candles.Count);
        foreach (var c in candles)
            chikou.Add(c.Close);

        return new IchimokuResultDto
        {
            TenkanSen = tenkan,
            KijunSen = kijun,
            SenkouSpanA = senkouA,
            SenkouSpanB = senkouB,
            ChikouSpan = chikou
        };
    }

    #endregion

    #region Shared Helpers

    /// <summary>
    /// (highest High + lowest Low) / 2 over a rolling window - the base
    /// calculation shared by Tenkan-sen, Kijun-sen, and Senkou Span B.
    /// </summary>
    private static List<double?> MidpointSeries(IReadOnlyList<CandleResponseDto> candles, int period)
    {
        var result = new List<double?>(new double?[candles.Count]);

        for (int i = period - 1; i < candles.Count; i++)
        {
            double highest = double.MinValue;
            double lowest = double.MaxValue;

            for (int j = i - period + 1; j <= i; j++)
            {
                highest = Math.Max(highest, candles[j].High);
                lowest = Math.Min(lowest, candles[j].Low);
            }

            result[i] = (highest + lowest) / 2.0;
        }

        return result;
    }

    /// <summary>
    /// EMA computed over an existing (possibly null-padded) series instead
    /// of candle closes. Used to build the MACD signal line from the MACD
    /// line itself.
    /// </summary>
    private static List<double?> EmaOfSeries(List<double?> series, int period)
    {
        var result = new List<double?>(new double?[series.Count]);

        // Find the first index that starts `period` consecutive non-null values.
        int start = -1;
        int consecutive = 0;

        for (int i = 0; i < series.Count; i++)
        {
            if (series[i].HasValue)
            {
                consecutive++;
                if (consecutive == period)
                {
                    start = i - period + 1;
                    break;
                }
            }
            else
            {
                consecutive = 0;
            }
        }

        if (start == -1)
            return result;

        double multiplier = 2.0 / (period + 1);

        double seed = 0;
        for (int i = start; i < start + period; i++)
            seed += series[i]!.Value;

        seed /= period;

        result[start + period - 1] = seed;

        double prev = seed;

        for (int i = start + period; i < series.Count; i++)
        {
            if (!series[i].HasValue)
            {
                // Gap in the source series - hold the last value rather
                // than break the chain.
                result[i] = prev;
                continue;
            }

            double val = (series[i]!.Value - prev) * multiplier + prev;
            result[i] = val;
            prev = val;
        }

        return result;
    }

    /// <summary>
    /// Simple Moving Average computed over an existing (possibly
    /// null-padded) series instead of candle closes. Used to build the
    /// Stochastic %K smoothing and %D line from raw/smoothed %K.
    /// </summary>
    private static List<double?> SmaOfSeries(List<double?> series, int period)
    {
        var result = new List<double?>(new double?[series.Count]);

        double sum = 0;
        var window = new Queue<double>();

        for (int i = 0; i < series.Count; i++)
        {
            if (series[i].HasValue)
            {
                window.Enqueue(series[i]!.Value);
                sum += series[i]!.Value;

                if (window.Count > period)
                    sum -= window.Dequeue();
            }

            result[i] = window.Count >= period ? sum / period : null;
        }

        return result;
    }

    private static void ValidatePeriod(int period)
    {
        if (period <= 0)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than zero.");
    }

    #endregion
}
