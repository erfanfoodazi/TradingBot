namespace Trading.Infrastructure.Database.Entities;

public sealed class WatchlistEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<WatchlistSymbolEntity> Symbols { get; set; } = [];
}