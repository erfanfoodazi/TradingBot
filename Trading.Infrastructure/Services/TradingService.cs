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

        if (response.Data is null)
            throw new Exception(response.Message);

        return response.Data;
    }

    public async Task<TradeResponseDto> SellAsync(TradeRequestDto request)
    {
        var response = await _python.SellAsync(request);

        if (response.Data is null)
            throw new Exception(response.Message);

        return response.Data;
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
}