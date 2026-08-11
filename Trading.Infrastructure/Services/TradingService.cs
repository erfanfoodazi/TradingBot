using Trading.Core.Exceptions;
using Trading.Core.Interfaces;
using Trading.Shared.Requests;
using Trading.Shared.Responses;

namespace Trading.Infrastructure.Services;

public class TradingService : ITradingService
{
    private readonly IPythonApiClient _python;

    public TradingService(IPythonApiClient python)
    {
        _python = python;
    }

    public async Task<TradeResponseDto> BuyAsync(TradeRequestDto request)
    {
        var response = await _python.BuyAsync(request);
        return EnsureData(response);
    }

    public async Task<TradeResponseDto> SellAsync(TradeRequestDto request)
    {
        var response = await _python.SellAsync(request);
        return EnsureData(response);
    }

    public async Task CloseAsync(ClosePositionRequestDto request)
    {
        await _python.CloseAsync(request);
    }

    public async Task<List<PositionResponseDto>> GetPositionsAsync()
    {
        var response = await _python.GetPositionsAsync();
        return response.Data ?? [];
    }

    public async Task<PendingOrderResponseDto> PlacePendingOrderAsync(
        PendingOrderRequestDto request)
    {
        var response = await _python.PlacePendingOrderAsync(request);
        return EnsureData(response);
    }

    public async Task<List<PendingOrderResponseDto>> GetPendingOrdersAsync()
    {
        var response = await _python.GetPendingOrdersAsync();
        return response.Data ?? [];
    }

    public async Task CancelPendingOrderAsync(long ticket)
    {
        await _python.CancelPendingOrderAsync(ticket);
    }

    public async Task ModifyPositionAsync(ModifyPositionRequestDto request)
    {
        await _python.ModifyPositionAsync(request);
    }

    public async Task<List<TradeHistoryResponseDto>> GetTradeHistoryAsync(
        TradeHistoryRequestDto request)
    {
        var response = await _python.GetTradeHistoryAsync(request);
        return response.Data ?? [];
    }

    public async Task<SymbolInfoResponseDto> GetSymbolInfoAsync(string symbol)
    {
        var response = await _python.GetSymbolInfoAsync(symbol);
        return EnsureData(response);
    }

    private static T EnsureData<T>(ApiResponseDto<T> response)
        => response.Data is null
            ? throw new PythonApiException(response.Message ?? "No data returned from Python API.")
            : response.Data;
}