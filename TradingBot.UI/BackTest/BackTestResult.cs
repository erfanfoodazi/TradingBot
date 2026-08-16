namespace TradingBot.UI.BackTest;

/// <summary>
/// Aggregate result of a backtest run plus the individual trades.
/// </summary>
public sealed class BackTestResult
{
    public string Symbol { get; set; } = string.Empty;

    public string Currency { get; set; } = "USD";

    public int CandleCount { get; set; }

    public double StartingBalance { get; set; }

    public double EndingBalance { get; set; }

    public double NetProfit => EndingBalance - StartingBalance;

    public int TotalTrades => Trades.Count;

    public int Wins => Trades.Count(t => t.Profit > 0);

    public int Losses => Trades.Count(t => t.Profit <= 0);

    public double WinRate => TotalTrades == 0 ? 0 : (double)Wins / TotalTrades;

    public double GrossProfit => Trades.Where(t => t.Profit > 0).Sum(t => t.Profit);

    public double GrossLoss => Math.Abs(Trades.Where(t => t.Profit <= 0).Sum(t => t.Profit));

    public double ProfitFactor => GrossLoss == 0 ? double.PositiveInfinity : GrossProfit / GrossLoss;

    /// <summary>Maximum peak-to-trough drop of the equity curve, in account currency.</summary>
    public double MaxDrawdown { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public TimeSpan Duration { get; set; }

    public List<BackTestTrade> Trades { get; set; } = [];

    public List<string> Log { get; set; } = [];
}