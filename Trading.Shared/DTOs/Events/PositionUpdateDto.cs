using System.Text.Json.Serialization;

namespace Trading.Shared.Events;

public sealed class PositionUpdateDto
{
    public long Ticket { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public double Volume { get; set; }

    [JsonPropertyName("price_open")]
    public double PriceOpen { get; set; }

    [JsonPropertyName("sl")]
    public double StopLoss { get; set; }

    [JsonPropertyName("tp")]
    public double TakeProfit { get; set; }

    public double Profit { get; set; }

    public DateTime Time { get; set; }
}