namespace Trading.Shared.Responses;

public sealed class PositionResponseDto
{
    public long Ticket { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public double Volume { get; set; }

    public string Type { get; set; } = string.Empty;

    public double PriceOpen { get; set; }

    public double StopLoss { get; set; }

    public double TakeProfit { get; set; }

    public double Profit { get; set; }
}