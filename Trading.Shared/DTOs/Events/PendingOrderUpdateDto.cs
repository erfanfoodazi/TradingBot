using Trading.Shared.Enums;

namespace Trading.Shared.Events;

public sealed class PendingOrderUpdateDto
{
    public long Ticket { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public PendingOrderType Type { get; set; }

    public double Volume { get; set; }

    public double Price { get; set; }

    public double? StopLoss { get; set; }

    public double? TakeProfit { get; set; }

    public OrderState State { get; set; } = OrderState.Placed;

    public double CurrentPrice { get; set; }

    public DateTime Time { get; set; }
}