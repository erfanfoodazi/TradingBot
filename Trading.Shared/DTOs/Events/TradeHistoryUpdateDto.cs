using Trading.Shared.Enums;

namespace Trading.Shared.Events;

public sealed class TradeHistoryUpdateDto
{
    public long Ticket { get; set; }

    public long? PositionId { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public DealType Type { get; set; }

    public double Volume { get; set; }

    public double Price { get; set; }

    public double Profit { get; set; }

    public DateTime Time { get; set; }

    public string Comment { get; set; } = string.Empty;
}