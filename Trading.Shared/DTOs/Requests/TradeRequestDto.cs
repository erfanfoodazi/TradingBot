namespace Trading.Shared.Requests;

public sealed class TradeRequestDto
{
    public string Symbol { get; set; } = string.Empty;

    public double Volume { get; set; }

    public double? StopLoss { get; set; }

    public double? TakeProfit { get; set; }
}