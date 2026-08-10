using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Trading.Core.Exceptions;
using Trading.Core.Interfaces;
using Trading.Infrastructure.Json;
using Trading.Infrastructure.Options;
using Trading.Shared.Enums;
using Trading.Shared.Requests;
using Trading.Shared.Responses;

namespace Trading.Infrastructure.Services;

public class PythonApiClient : IPythonApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly HttpClient _httpClient;

    public PythonApiClient(
        HttpClient httpClient,
        IOptions<PythonApiOptions> options)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.BaseUrl);
    }

    public Task<ApiResponseDto<List<CandleResponseDto>>> GetCandlesAsync(
        CandleHistoryRequestDto request)
        => SendAsync<ApiResponseDto<List<CandleResponseDto>>>(
            () => _httpClient.PostAsJsonAsync("/api/candles", request, JsonOptions));

    public async Task<ApiResponseDto<List<SymbolResponseDto>>> GetSymbolsAsync()
    {
        var api = await SendAsync<ApiResponseDto<List<string>>>(
            () => _httpClient.GetAsync("/api/symbols"));

        return new ApiResponseDto<List<SymbolResponseDto>>
        {
            Success = api.Success,
            Message = api.Message,
            Data = api.Data?
                .Select(name => new SymbolResponseDto { Name = name })
                .ToList() ?? []
        };
    }

    public Task<ApiResponseDto<TradeResponseDto>> BuyAsync(TradeRequestDto request)
        => SendAsync<ApiResponseDto<TradeResponseDto>>(
            () => _httpClient.PostAsJsonAsync("/api/orders/buy", request, JsonOptions));

    public Task<ApiResponseDto<TradeResponseDto>> SellAsync(TradeRequestDto request)
        => SendAsync<ApiResponseDto<TradeResponseDto>>(
            () => _httpClient.PostAsJsonAsync("/api/orders/sell", request, JsonOptions));

    public Task<ApiResponseDto<object>> CloseAsync(ClosePositionRequestDto request)
        => SendAsync<ApiResponseDto<object>>(
            () => _httpClient.PostAsJsonAsync("/api/orders/close", request, JsonOptions));

    public Task<ApiResponseDto<List<PositionResponseDto>>> GetPositionsAsync()
        => SendAsync<ApiResponseDto<List<PositionResponseDto>>>(
            () => _httpClient.GetAsync("/api/orders/positions"));

    public async Task<ApiResponseDto<HealthResponseDto>> HealthAsync()
    {
        var response = await SendAsync<ApiResponseDto<HealthResponseDto>>(
            () => _httpClient.GetAsync("/health"));

        if (response.Data is not null)
            response.Data.Connected = response.Success;

        return response;
    }

    public Task<ApiResponseDto<AccountResponseDto>> GetAccountAsync()
        => SendAsync<ApiResponseDto<AccountResponseDto>>(
            () => _httpClient.GetAsync("/api/account"));

    public Task<ApiResponseDto<PendingOrderResponseDto>> PlacePendingOrderAsync(
        PendingOrderRequestDto request)
        => SendAsync<ApiResponseDto<PendingOrderResponseDto>>(
            () => _httpClient.PostAsJsonAsync("/api/orders/pending", request, JsonOptions));

    public Task<ApiResponseDto<List<PendingOrderResponseDto>>> GetPendingOrdersAsync()
        => SendAsync<ApiResponseDto<List<PendingOrderResponseDto>>>(
            () => _httpClient.GetAsync("/api/orders/pending-orders"));

    public Task<ApiResponseDto<object>> CancelPendingOrderAsync(long ticket)
        => SendAsync<ApiResponseDto<object>>(
            () => _httpClient.PostAsJsonAsync(
                "/api/orders/cancel-pending",
                new CancelPendingOrderRequestDto { Ticket = ticket },
                JsonOptions));

    public Task<ApiResponseDto<object>> ModifyPositionAsync(
        ModifyPositionRequestDto request)
        => SendAsync<ApiResponseDto<object>>(
            () => _httpClient.PostAsJsonAsync("/api/orders/modify", request, JsonOptions));

    public Task<ApiResponseDto<List<TradeHistoryResponseDto>>> GetTradeHistoryAsync(
        TradeHistoryRequestDto request)
        => SendAsync<ApiResponseDto<List<TradeHistoryResponseDto>>>(
            () => _httpClient.PostAsJsonAsync("/api/orders/history", request, JsonOptions));

    private async Task<ApiResponseDto<List<SymbolResponseDto>>> GetSymbolsCore()
    {
        var api = await SendAsync<ApiResponseDto<List<string>>>(
            () => _httpClient.GetAsync("/api/symbols"));

        return new ApiResponseDto<List<SymbolResponseDto>>
        {
            Success = api.Success,
            Message = api.Message,
            Data = api.Data?
                .Select(name => new SymbolResponseDto { Name = name })
                .ToList() ?? []
        };
    }

    /// <summary>
    /// Sends a request, always unwraps the ApiResponse envelope, and throws a
    /// <see cref="PythonApiException"/> for transport/validation failures.
    /// </summary>
    private async Task<T> SendAsync<T>(Func<Task<HttpResponseMessage>> send)
        where T : class
    {
        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await send();
        }
        catch (HttpRequestException ex)
        {
            throw new PythonApiException($"Could not reach Python API: {ex.Message}", innerException: ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new PythonApiException("Python API request timed out.", innerException: ex);
        }

        var body = await httpResponse.Content.ReadAsStringAsync();

        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new PythonApiException(ExtractErrorMessage(body, httpResponse));
        }

        T? api;
        try
        {
            api = JsonSerializer.Deserialize<T>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new PythonApiException(
                $"Invalid response from Python API (status {(int)httpResponse.StatusCode}).",
                innerException: ex);
        }

        if (api is null)
            throw new PythonApiException("Empty response from Python API.");

        if (api is IApiResponse apiResponse && !apiResponse.Success)
            throw new PythonApiException(apiResponse.Message ?? "Python API returned an error.");

        return api;
    }

    /// <summary>
    /// Extracts a useful message from a non-success response body (the Python
    /// API wraps errors in a JSON envelope, but we never trust that blindly).
    /// </summary>
    private static string ExtractErrorMessage(string body, HttpResponseMessage response)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var msg) &&
                    msg.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(msg.GetString()))
                {
                    return $"Python API error ({(int)response.StatusCode}): {msg.GetString()}";
                }
            }
            catch (JsonException)
            {
                // Fall through to the raw body below.
            }
        }

        var preview = body.Length > 200 ? body[..200] : body;
        return $"Python API error ({(int)response.StatusCode}): {preview}";
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };

        options.Converters.Add(new FlexibleJsonEnumConverter<PendingOrderType>());
        options.Converters.Add(new FlexibleJsonEnumConverter<OrderState>());
        options.Converters.Add(new FlexibleJsonEnumConverter<DealType>());
        options.Converters.Add(new UnixTimestampConverter());

        return options;
    }
}