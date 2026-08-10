namespace Trading.Shared.Events;

public sealed class WatchlistUpdateDto
{
    public string Name { get; set; } = string.Empty;

    public List<string> Symbols { get; set; } = [];
}