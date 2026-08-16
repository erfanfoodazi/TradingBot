namespace TradingBot.UI.BackTest;

/// <summary>
/// A single simulated trade produced by the backtest engine.
/// </summary>
public sealed class BackTestTrade
{
    public int Id { get; set; }

    public string Symbol { get; set; } = string.Empty;

    /// <summary>True = SELL, False = BUY.</summary>
    public bool Sell { get; set; }

    public DateTime OpenTime { get; set; }

    public double EntryPrice { get; set; }

    public double StopLoss { get; set; }

    public double TakeProfit { get; set; }

    public double Volume { get; set; }

    public DateTime CloseTime { get; set; }

    public double ExitPrice { get; set; }

    /// <summary>SL, TP or EndOfData.</summary>
    public string ExitReason { get; set; } = string.Empty;

    /// <summary>Realized P/L in account currency.</summary>
    public double Profit { get; set; }

    public string Side => Sell ? "SELL" : "BUY";
}