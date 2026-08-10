using System.Text.Json.Serialization;

namespace Trading.Shared.Responses;

public sealed class CandleResponseDto
{
    public DateTime Time { get; set; }

    public double Open { get; set; }

    public double High { get; set; }

    public double Low { get; set; }

    public double Close { get; set; }

    [JsonPropertyName("tick_volume")]
    public long TickVolume { get; set; }
}