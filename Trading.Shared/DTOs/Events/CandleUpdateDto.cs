namespace Trading.Shared.Events;

public sealed class CandleUpdateDto
{
    public string Symbol { get; set; } = string.Empty;

    public string Timeframe { get; set; } = string.Empty;

    public DateTime Time { get; set; }

    public double Open { get; set; }

    public double High { get; set; }

    public double Low { get; set; }

    public double Close { get; set; }

    public long TickVolume { get; set; }
}