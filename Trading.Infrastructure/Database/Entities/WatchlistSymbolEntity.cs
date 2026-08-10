namespace Trading.Infrastructure.Database.Entities;

public sealed class WatchlistSymbolEntity
{
    public int Id { get; set; }

    public int WatchlistId { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public WatchlistEntity? Watchlist { get; set; }
}