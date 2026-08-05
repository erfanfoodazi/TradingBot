using Trading.Shared.Requests;
using Trading.Shared.Responses;

namespace Trading.Core.Interfaces;

public interface ITradingService
{
    Task<TradeResponseDto> BuyAsync(
        TradeRequestDto request);

    Task<TradeResponseDto> SellAsync(
        TradeRequestDto request);

    Task CloseAsync(
        ClosePositionRequestDto request);

    Task<List<PositionResponseDto>> GetPositionsAsync();
}