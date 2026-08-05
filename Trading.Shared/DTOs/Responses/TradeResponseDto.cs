namespace Trading.Shared.Responses;

public sealed class TradeResponseDto
{
    public string Symbol { get; set; } = string.Empty;

    public long Ticket { get; set; }
}