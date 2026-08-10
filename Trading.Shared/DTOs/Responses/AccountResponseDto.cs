using System.Text.Json.Serialization;

namespace Trading.Shared.Responses;

public sealed class AccountResponseDto
{
    public long Login { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string Server { get; set; } = string.Empty;

    [JsonPropertyName("trade_mode")]
    public string TradeMode { get; set; } = string.Empty;

    public int Leverage { get; set; }

    [JsonPropertyName("trade_allowed")]
    public bool TradeAllowed { get; set; }

    [JsonPropertyName("margin_mode")]
    public string MarginMode { get; set; } = string.Empty;

    public double Balance { get; set; }

    public double Equity { get; set; }

    public double Margin { get; set; }

    [JsonPropertyName("margin_free")]
    public double FreeMargin { get; set; }

    [JsonPropertyName("margin_level")]
    public double MarginLevel { get; set; }

    public double Profit { get; set; }

    public double Credit { get; set; }

    public bool Connected { get; set; }
}