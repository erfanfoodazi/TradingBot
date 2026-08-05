using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Trading.Core.Interfaces;
using Trading.Shared.Requests;
using Trading.Shared.Responses;

namespace Trading.Infrastructure.Services
{
    public class PythonApiClient : IPythonApiClient
    {
        private readonly HttpClient _httpClient;

        public PythonApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;

            _httpClient.BaseAddress =
                new Uri("http://127.0.0.1:8000");
        }
        public async Task<ApiResponseDto<TradeResponseDto>> BuyAsync(TradeRequestDto request)
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/orders/buy",
                request);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<ApiResponseDto<TradeResponseDto>>()
                   ?? throw new Exception("Invalid response.");
        }

        public async Task<ApiResponseDto<object>> CloseAsync(ClosePositionRequestDto request)
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/orders/close",
                request);
            
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>()
                   ?? throw new Exception("Invalid response.");
        }

        public async Task<ApiResponseDto<List<CandleResponseDto>>> GetCandlesAsync(CandleHistoryRequestDto request)
        {

            var response = await _httpClient.PostAsJsonAsync(
                "/api/candles",
                request);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<ApiResponseDto<List<CandleResponseDto>>>()
                   ?? throw new Exception("Invalid response.");
            }

        public async Task<ApiResponseDto<List<PositionResponseDto>>> GetPositionsAsync()
        {
            return await _httpClient.GetFromJsonAsync<ApiResponseDto<List<PositionResponseDto>>>(
                "/api/orders/positions")
                ?? throw new Exception("Invalid response.");
        }

        public async Task<ApiResponseDto<List<SymbolResponseDto>>> GetSymbolsAsync()
        {
            var response =
                await _httpClient.GetFromJsonAsync<ApiResponseDto<List<SymbolResponseDto>>>
                (
                    "/api/symbols"
                );

            return response!;
        }

        public async Task<ApiResponseDto<HealthResponseDto>> HealthAsync()
        {
            var response =
                await _httpClient.GetFromJsonAsync<ApiResponseDto<HealthResponseDto>>
                (
                    "/health"
                );

            return response!;
        }

        public  async Task<ApiResponseDto<TradeResponseDto>> SellAsync(TradeRequestDto request)
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/orders/sell",
                request);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<ApiResponseDto<TradeResponseDto>>()
                   ?? throw new Exception("Invalid response.");
        }

        //private static T EnsureSuccess<T>(ApiResponseDto<T> response)
        //{
        //    if (!response.Success)
        //        throw new PythonApiException(response.Message);

        //    return response.Data!;
        //}
    }
}
