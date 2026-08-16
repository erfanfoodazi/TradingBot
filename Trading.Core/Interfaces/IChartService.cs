using Trading.Core.Indicators;
using Trading.Shared.Events;
using Trading.Shared.Responses;

namespace Trading.Core.Interfaces;

public interface IChartService
{
    void LoadCandles(
        IEnumerable<CandleResponseDto> candles);

    /// <summary>
    /// Sets the set of technical indicators to overlay on the chart. The chart
    /// is re-rendered immediately and on every subsequent candle update.
    /// </summary>
    void SetIndicators(
        IEnumerable<IndicatorType> indicators);

    /// <summary>
    /// Appends a new candle, or replaces the last candle when the update
    /// belongs to the same (in-progress) bar.
    /// </summary>
    void AddCandle(
        CandleUpdateDto candle);

    void Clear();

    /// <summary>
    /// Overlays backtest entry / SL / TP markers on the chart.
    /// </summary>
    void SetBackTestMarkers(
        IEnumerable<BackTestMarker> markers);

    void ClearBackTestMarkers();
}