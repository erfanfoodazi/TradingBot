using Trading.Shared.Requests;
using Trading.Shared.Responses;

namespace Trading.Core.Interfaces;

public interface IPythonApiClient
{
    Task<ApiResponseDto<List<CandleResponseDto>>> GetCandlesAsync(
        CandleHistoryRequestDto request);

    Task<ApiResponseDto<List<SymbolResponseDto>>> GetSymbolsAsync();

    Task<ApiResponseDto<SymbolInfoResponseDto>> GetSymbolInfoAsync(
        string symbol);

    Task<ApiResponseDto<TradeResponseDto>> BuyAsync(
        TradeRequestDto request);

    Task<ApiResponseDto<TradeResponseDto>> SellAsync(
        TradeRequestDto request);

    Task<ApiResponseDto<object>> CloseAsync(
        ClosePositionRequestDto request);

    Task<ApiResponseDto<List<PositionResponseDto>>> GetPositionsAsync();

    Task<ApiResponseDto<HealthResponseDto>> HealthAsync();

    Task<ApiResponseDto<AccountResponseDto>> GetAccountAsync();

    Task<ApiResponseDto<PendingOrderResponseDto>> PlacePendingOrderAsync(
        PendingOrderRequestDto request);

    Task<ApiResponseDto<List<PendingOrderResponseDto>>> GetPendingOrdersAsync();

    Task<ApiResponseDto<object>> CancelPendingOrderAsync(
        long ticket);

    Task<ApiResponseDto<object>> ModifyPositionAsync(
        ModifyPositionRequestDto request);

    Task<ApiResponseDto<List<TradeHistoryResponseDto>>> GetTradeHistoryAsync(
        TradeHistoryRequestDto request);
}