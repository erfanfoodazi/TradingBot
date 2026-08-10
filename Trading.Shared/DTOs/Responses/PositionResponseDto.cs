using System.Text.Json.Serialization;

namespace Trading.Shared.Responses;

public sealed class PositionResponseDto
{
    public long Ticket { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public double Volume { get; set; }

    public string Type { get; set; } = string.Empty;

    public double PriceOpen { get; set; }

    [JsonPropertyName("sl")]
    public double StopLoss { get; set; }

    [JsonPropertyName("tp")]
    public double TakeProfit { get; set; }

    public double Profit { get; set; }

    /// <summary>UI flag: whether the row is ticked (e.g. for batch closing).</summary>
    public bool IsSelected { get; set; }
}