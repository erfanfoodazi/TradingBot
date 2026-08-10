using Trading.Shared.Events;
using Trading.Shared.Responses;

namespace Trading.Core.Interfaces;

public interface IChartService
{
    void LoadCandles(
        IEnumerable<CandleResponseDto> candles);

    /// <summary>
    /// Appends a new candle, or replaces the last candle when the update
    /// belongs to the same (in-progress) bar.
    /// </summary>
    void AddCandle(
        CandleUpdateDto candle);

    void Clear();
}