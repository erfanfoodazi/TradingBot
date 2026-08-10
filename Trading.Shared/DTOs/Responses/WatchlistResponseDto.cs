namespace Trading.Shared.Responses;

public sealed class WatchlistResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public List<string> Symbols { get; set; } = [];
}