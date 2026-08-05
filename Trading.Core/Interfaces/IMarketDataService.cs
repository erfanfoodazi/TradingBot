using Trading.Shared.Responses;

namespace Trading.Core.Interfaces;

public interface IMarketDataService
{
    Task<List<CandleResponseDto>> GetCandlesAsync(
        string symbol,
        string timeframe,
        int count);

    Task<List<SymbolResponseDto>> GetSymbolsAsync();

    Task<HealthResponseDto> HealthAsync();
}