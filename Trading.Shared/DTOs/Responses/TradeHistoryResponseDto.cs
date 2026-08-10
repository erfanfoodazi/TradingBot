using System.Text.Json.Serialization;
using Trading.Shared.Enums;

namespace Trading.Shared.Responses;

public sealed class TradeHistoryResponseDto
{
    public long Ticket { get; set; }

    [JsonPropertyName("position_id")]
    public long? PositionId { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public DealType Type { get; set; }

    public double Volume { get; set; }

    public double Price { get; set; }

    public double Profit { get; set; }

    public double Commission { get; set; }

    public double Swap { get; set; }

    public double Fee { get; set; }

    public DateTime Time { get; set; }

    public string Comment { get; set; } = string.Empty;
}