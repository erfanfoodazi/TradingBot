namespace Trading.Shared.Events;

public sealed class TickUpdateDto
{
    public string Symbol { get; set; } = string.Empty;

    public double Bid { get; set; }

    public double Ask { get; set; }

    public DateTime Time { get; set; }
}