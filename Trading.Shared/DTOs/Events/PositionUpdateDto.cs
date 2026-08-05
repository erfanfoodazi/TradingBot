namespace Trading.Shared.Events;

public sealed class PositionUpdateDto
{
    public long Ticket { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public double Profit { get; set; }

    public double Volume { get; set; }
}