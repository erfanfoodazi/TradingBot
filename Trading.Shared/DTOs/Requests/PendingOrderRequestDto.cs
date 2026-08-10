using Trading.Shared.Enums;

namespace Trading.Shared.Requests;

public sealed class PendingOrderRequestDto
{
    public string Symbol { get; set; } = string.Empty;

    public PendingOrderType Type { get; set; } = PendingOrderType.BuyLimit;

    public double Volume { get; set; } = 0.01;

    public double Price { get; set; }

    public double? StopLoss { get; set; }

    public double? TakeProfit { get; set; }

    public DateTime? Expiration { get; set; }

    public string? Comment { get; set; }
}