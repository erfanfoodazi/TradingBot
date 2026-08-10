using System.Text.Json.Serialization;
using Trading.Shared.Enums;

namespace Trading.Shared.Responses;

public sealed class PendingOrderResponseDto
{
    public long Ticket { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public PendingOrderType Type { get; set; }

    public double Volume { get; set; }

    public double Price { get; set; }

    [JsonPropertyName("sl")]
    public double? StopLoss { get; set; }

    [JsonPropertyName("tp")]
    public double? TakeProfit { get; set; }

    public OrderState State { get; set; } = OrderState.Placed;

    public DateTime? Expiration { get; set; }
}