using System.Text.Json.Serialization;

namespace Trading.Shared.Responses;

/// <summary>
/// Symbol trade specs returned by the Python <c>/api/symbols/{symbol}</c>
/// endpoint. Used to translate a monetary risk / reward target (a percentage
/// of the account balance) into SL/TP price levels so the realized P/L at
/// those levels matches the target regardless of the symbol or volume.
/// </summary>
public sealed class SymbolInfoResponseDto
{
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("digits")]
    public int Digits { get; set; }

    /// <summary>Smallest point size, in price units.</summary>
    [JsonPropertyName("point")]
    public double Point { get; set; }

    /// <summary>MT5 trade_tick_size.</summary>
    [JsonPropertyName("tick_size")]
    public double TickSize { get; set; }

    /// <summary>
    /// MT5 trade_tick_value: account-currency value of one tick per 1.0 lot.
    /// </summary>
    [JsonPropertyName("tick_value")]
    public double TickValue { get; set; }

    /// <summary>MT5 trade_contract_size (base units of one lot).</summary>
    [JsonPropertyName("contract_size")]
    public double ContractSize { get; set; }

    /// <summary>Profit currency of the symbol.</summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    /// <summary>MT5 volume_min: minimum trade volume in lots.</summary>
    [JsonPropertyName("volume_min")]
    public double VolumeMin { get; set; }

    /// <summary>MT5 volume_max: maximum trade volume in lots.</summary>
    [JsonPropertyName("volume_max")]
    public double VolumeMax { get; set; }

    /// <summary>MT5 volume_step: volume normalization step in lots.</summary>
    [JsonPropertyName("volume_step")]
    public double VolumeStep { get; set; }
}