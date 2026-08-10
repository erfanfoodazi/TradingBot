namespace Trading.Shared.Requests;

public sealed class TradeRequestDto
{
    public string Symbol { get; set; } = string.Empty;

    public double Volume { get; set; } = 0.01;

    public double? StopLoss { get; set; } = 0;

    public double? TakeProfit { get; set; } = 0;
}