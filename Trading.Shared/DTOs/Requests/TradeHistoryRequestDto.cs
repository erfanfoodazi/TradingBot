namespace Trading.Shared.Requests;

public sealed class TradeHistoryRequestDto
{
    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public int Count { get; set; } = 100;
}