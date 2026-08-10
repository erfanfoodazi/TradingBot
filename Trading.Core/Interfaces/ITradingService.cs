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

    Task<PendingOrderResponseDto> PlacePendingOrderAsync(
        PendingOrderRequestDto request);

    Task<List<PendingOrderResponseDto>> GetPendingOrdersAsync();

    Task CancelPendingOrderAsync(long ticket);

    Task ModifyPositionAsync(
        ModifyPositionRequestDto request);

    Task<List<TradeHistoryResponseDto>> GetTradeHistoryAsync(
        TradeHistoryRequestDto request);
}