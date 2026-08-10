using Trading.Core.Interfaces;
using Trading.Shared.Events;
using Trading.Shared.Responses;

namespace Trading.Infrastructure.Services;

public class AccountService : IAccountService
{
    private readonly IPythonApiClient _python;
    private readonly IRealtimeService _realtime;

    public AccountService(
        IPythonApiClient python,
        IRealtimeService realtime)
    {
        _python = python;
        _realtime = realtime;
        _realtime.AccountReceived += OnAccountReceived;
    }

    public event Action<AccountUpdateDto>? AccountUpdated;

    public async Task<AccountResponseDto> GetAccountInfoAsync()
    {
        var response = await _python.GetAccountAsync();
        return response.Data
               ?? throw new Trading.Core.Exceptions.PythonApiException(
                   response.Message ?? "No account data returned from Python API.");
    }

    public async Task UpdateAccountAsync()
    {
        var info = await GetAccountInfoAsync();
        AccountUpdated?.Invoke(new AccountUpdateDto
        {
            Login = info.Login,
            Currency = info.Currency,
            Server = info.Server,
            Leverage = info.Leverage,
            Balance = info.Balance,
            Equity = info.Equity,
            Margin = info.Margin,
            FreeMargin = info.FreeMargin,
            MarginLevel = info.MarginLevel,
            Profit = info.Profit,
            Time = DateTime.UtcNow,
        });
    }

    private void OnAccountReceived(AccountUpdateDto update)
        => AccountUpdated?.Invoke(update);
}