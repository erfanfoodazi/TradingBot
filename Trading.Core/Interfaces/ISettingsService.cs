using Trading.Shared.Requests;
using Trading.Shared.Responses;

namespace Trading.Core.Interfaces;

/// <summary>
/// Key/value settings persisted in SQL Server (extensible by design:
/// consumers can store any application setting without schema changes).
/// </summary>
public interface ISettingsService
{
    Task<List<SettingResponseDto>> GetAllAsync();

    Task<string?> GetAsync(string key);

    Task SetAsync(SettingRequestDto request);
}