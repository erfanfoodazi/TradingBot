using Trading.Shared.Events;
using Trading.Shared.Responses;

namespace Trading.Core.Interfaces;

public interface IChartService
{
    void LoadCandles(
        IEnumerable<CandleResponseDto> candles);

    void AddCandle(
        CandleUpdateDto candle);

    void Clear();
}