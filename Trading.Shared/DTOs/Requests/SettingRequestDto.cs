namespace Trading.Shared.Requests;

public sealed class SettingRequestDto
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}