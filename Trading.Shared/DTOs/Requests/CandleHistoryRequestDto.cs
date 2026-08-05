namespace Trading.Shared.Requests;

public sealed class CandleHistoryRequestDto
{
    public string Symbol { get; set; } = string.Empty;

    public string Timeframe { get; set; } = "M1";

    public int Count { get; set; } = 500;
}