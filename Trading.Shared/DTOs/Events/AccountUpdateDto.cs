using System.Text.Json.Serialization;

namespace Trading.Shared.Events;

public sealed class AccountUpdateDto
{
    public long Login { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string Server { get; set; } = string.Empty;

    public int Leverage { get; set; }

    public double Balance { get; set; }

    public double Equity { get; set; }

    public double Margin { get; set; }

    [JsonPropertyName("margin_free")]
    public double FreeMargin { get; set; }

    [JsonPropertyName("margin_level")]
    public double MarginLevel { get; set; }

    public double Profit { get; set; }

    [JsonPropertyName("trade_allowed")]
    public bool TradeAllowed { get; set; }

    public DateTime Time { get; set; }
}