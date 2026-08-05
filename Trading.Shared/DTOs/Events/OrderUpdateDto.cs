namespace Trading.Shared.Events;

public sealed class OrderUpdateDto
{
    public long Ticket { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}