using System.Net.Http;
using System.Net.Http.Json;
using Trading.Core.Interfaces;
using Trading.Shared.Requests;
using Trading.Shared.Responses;

namespace TradingBot.UI.Services;

public class PythonApiClient : IPythonApiClient
{
    private readonly HttpClient _httpClient;

    public PythonApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ApiResponseDto<List<CandleResponseDto>>> GetCandlesAsync(CandleHistoryRequestDto request)
    {
        var url =
            $"/api/candles/history?symbol={request.Symbol}" +
            $"&timeframe={request.Timeframe}" +
            $"&count={request.Count}";

        var response =
            await _httpClient.GetFromJsonAsync<ApiResponseDto<List<CandleResponseDto>>>(url);

        return response
               ?? throw new Exception("No response received from Python API.");
    }

    public async Task<ApiResponseDto<List<SymbolResponseDto>>> GetSymbolsAsync()
    {
        var response =
            await _httpClient.GetFromJsonAsync<ApiResponseDto<List<SymbolResponseDto>>>(
                "/api/symbols");

        return response
               ?? throw new Exception("No response received from Python API.");
    }

    public async Task<ApiResponseDto<TradeResponseDto>> BuyAsync(TradeRequestDto request)
    {
        var httpResponse =
            await _httpClient.PostAsJsonAsync("/api/orders/buy", request);

        httpResponse.EnsureSuccessStatusCode();

        var response =
            await httpResponse.Content.ReadFromJsonAsync<ApiResponseDto<TradeResponseDto>>();

        return response
               ?? throw new Exception("No response received from Python API.");
    }

    public async Task<ApiResponseDto<TradeResponseDto>> SellAsync(TradeRequestDto request)
    {
        var httpResponse =
            await _httpClient.PostAsJsonAsync("/api/orders/sell", request);

        httpResponse.EnsureSuccessStatusCode();

        var response =
            await httpResponse.Content.ReadFromJsonAsync<ApiResponseDto<TradeResponseDto>>();

        return response
               ?? throw new Exception("No response received from Python API.");
    }

    public async Task<ApiResponseDto<object>> CloseAsync(ClosePositionRequestDto request)
    {
        var httpResponse =
            await _httpClient.PostAsJsonAsync("/api/orders/close", request);

        httpResponse.EnsureSuccessStatusCode();

        var response =
            await httpResponse.Content.ReadFromJsonAsync<ApiResponseDto<object>>();

        return response
               ?? throw new Exception("No response received from Python API.");
    }

    public async Task<ApiResponseDto<List<PositionResponseDto>>> GetPositionsAsync()
    {
        var response =
            await _httpClient.GetFromJsonAsync<ApiResponseDto<List<PositionResponseDto>>>(
                "/api/orders/positions");

        return response
               ?? throw new Exception("No response received from Python API.");
    }

    public async Task<ApiResponseDto<HealthResponseDto>> HealthAsync()
    {
        var response =
            await _httpClient.GetFromJsonAsync<ApiResponseDto<HealthResponseDto>>(
                "/health");

        return response
               ?? throw new Exception("No response received from Python API.");
    }
}