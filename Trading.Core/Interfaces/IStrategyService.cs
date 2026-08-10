using Trading.Shared.Requests;
using Trading.Shared.Responses;

/// <summary>
/// Manages trading strategies. Strategies are persisted in SQL Server so they
/// can be created, edited or activated without code changes (extensible via
/// the free-form ParametersJson blob, e.g. for future AI/backtesting engines).
/// </summary>
public interface IStrategyService
{
    Task<List<StrategyResponseDto>> GetAllAsync();

    Task<StrategyResponseDto> SaveAsync(StrategyRequestDto request);

    Task DeleteAsync(int id);
}