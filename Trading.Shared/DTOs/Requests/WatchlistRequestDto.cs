namespace Trading.Shared.Requests;

public sealed class WatchlistRequestDto
{
    public int? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public List<string> Symbols { get; set; } = [];
}