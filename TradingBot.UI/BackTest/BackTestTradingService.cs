using Trading.Core.Interfaces;
using Trading.Shared.Requests;
using Trading.Shared.Responses;

namespace TradingBot.UI.BackTest;

/// <summary>
/// Simulated broker used by the backtest engine.
///
/// Instead of placing real orders it records every trade with the entry price
/// taken from the simulated tick the engine feeds just before execution, plus
/// the SL/TP and volume computed by the strategy. The engine later resolves
/// SL/TP hits from subsequent candles.
/// </summary>
public sealed class BackTestTradingService : ITradingService
{
    /// <summary>Bid of the simulated tick currently being processed.</summary>
    public double CurrentBid { get; set; }

    /// <summary>Ask of the simulated tick currently being processed.</summary>
    public double CurrentAsk { get; set; }

    public SymbolInfoResponseDto SymbolInfo { get; set; } = new()
    {
        Symbol = string.Empty,
        Digits = 5,
        Point = 0.00001,
        TickSize = 0.00001,
        TickValue = 1.0,
        ContractSize = 100_000,
        Currency = "USD",
        VolumeMin = 0.01,
        VolumeMax = 1000,
        VolumeStep = 0.01,
    };

    public List<BackTestTrade> Trades { get; } = [];

    private long _nextTicket = 1000;

    public Task<TradeResponseDto> BuyAsync(TradeRequestDto request)
    {
        var trade = new BackTestTrade
        {
            Id = (int)_nextTicket++,
            Symbol = request.Symbol,
            Sell = false,
            EntryPrice = CurrentAsk,
            StopLoss = request.StopLoss ?? 0,
            TakeProfit = request.TakeProfit ?? 0,
            Volume = request.Volume,
            OpenTime = DateTime.UtcNow,
        };
        Trades.Add(trade);

        return Task.FromResult(new TradeResponseDto
        {
            Symbol = request.Symbol,
            Ticket = trade.Id,
        });
    }

    public Task<TradeResponseDto> SellAsync(TradeRequestDto request)
    {
        var trade = new BackTestTrade
        {
            Id = (int)_nextTicket++,
            Symbol = request.Symbol,
            Sell = true,
            EntryPrice = CurrentBid,
            StopLoss = request.StopLoss ?? 0,
            TakeProfit = request.TakeProfit ?? 0,
            Volume = request.Volume,
            OpenTime = DateTime.UtcNow,
        };
        Trades.Add(trade);

        return Task.FromResult(new TradeResponseDto
        {
            Symbol = request.Symbol,
            Ticket = trade.Id,
        });
    }

    public Task<SymbolInfoResponseDto> GetSymbolInfoAsync(string symbol)
    {
        SymbolInfo.Symbol = symbol;
        return Task.FromResult(SymbolInfo);
    }

    public Task CloseAsync(ClosePositionRequestDto request) => Task.CompletedTask;

    public Task<List<PositionResponseDto>> GetPositionsAsync()
        => Task.FromResult(new List<PositionResponseDto>());

    public Task<PendingOrderResponseDto> PlacePendingOrderAsync(PendingOrderRequestDto request)
        => throw new NotSupportedException("Pending orders are not available in backtest mode.");

    public Task<List<PendingOrderResponseDto>> GetPendingOrdersAsync()
        => Task.FromResult(new List<PendingOrderResponseDto>());

    public Task CancelPendingOrderAsync(long ticket) => Task.CompletedTask;

    public Task ModifyPositionAsync(ModifyPositionRequestDto request) => Task.CompletedTask;

    public Task<List<TradeHistoryResponseDto>> GetTradeHistoryAsync(TradeHistoryRequestDto request)
        => Task.FromResult(new List<TradeHistoryResponseDto>());
}