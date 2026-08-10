using Trading.Shared.Events;
using Trading.Shared.Responses;

namespace Trading.Core.Interfaces;

public interface IAccountService
{
    Task<AccountResponseDto> GetAccountInfoAsync();

    Task UpdateAccountAsync();

    event Action<AccountUpdateDto>? AccountUpdated;
}