using Trading.Core.Interfaces;
using Trading.Shared.Events;
using Trading.Shared.Responses;

namespace TradingBot.UI.BackTest;

/// <summary>
/// Simulated account used by the backtest engine.
///
/// All methods complete synchronously so the strategy's async trade path
/// (async void) runs to completion on the calling thread and trades are
/// recorded before the backtest engine advances to the next candle.
/// </summary>
public sealed class BackTestAccountService : IAccountService
{
    public double Balance { get; set; } = 10_000;

    public string Currency { get; set; } = "USD";

    public long Login { get; set; } = 12345;

    public string Server { get; set; } = "BackTest";

    public int Leverage { get; set; } = 100;

    public Task<AccountResponseDto> GetAccountInfoAsync()
        => Task.FromResult(new AccountResponseDto
        {
            Login = Login,
            Currency = Currency,
            Server = Server,
            Leverage = Leverage,
            Balance = Balance,
            Equity = Balance,
            TradeAllowed = true,
            Connected = true,
        });

    public Task UpdateAccountAsync() => Task.CompletedTask;

    public event Action<AccountUpdateDto>? AccountUpdated;
}