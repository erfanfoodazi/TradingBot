using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Trading.Shared.Events;
using Trading.Shared.Responses;
using TradingBot.UI.Strategy;

namespace TradingBot.UI.BackTest;

/// <summary>
/// Runs the <see cref="ManualStrategy"/> against historical candles exactly the
/// way it would consume live data: every candle is fed into the strategy one by
/// one as it closes, and the candle's High/Low is used to simulate the ticks
/// that drive the entry.
///
/// Touch (Shadow) entries:
///   SELL -> the trade opens the moment the forming candle's High touches the
///           calculated entry price (Bid).
///   BUY  -> the trade opens the moment the forming candle's Low touches the
///           calculated entry price (Ask).
///
/// Once a trade is open its Stop Loss / Take Profit are resolved against the
/// HIGH/LOW of every following candle; whichever level is reached first (or is
/// closer to the candle open when both are reached) closes the trade.
/// </summary>
public static class BackTestEngine
{
    /// <summary>Spread applied to simulated Bid/Ask when the entry is touched.</summary>
    public const double DefaultSpread = 0.0002;

    private static readonly string[] TimeFormats =
    [
        "yyyy.MM.dd HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
        "MM/dd/yyyy HH:mm:ss",
        "dd.MM.yyyy HH:mm:ss",
        "yyyy.MM.dd",
        "yyyy-MM-dd",
        "MM/dd/yyyy",
        "dd.MM.yyyy",
        "MMM dd yyyy HH:mm:ss",
        "MMM d yyyy HH:mm:ss",
        "yyyy/MM/dd HH:mm:ss",
    ];

    #region CSV

    /// <summary>
    /// Loads OHLC candles from a CSV file.
    ///
    /// Header row is detected automatically; when present the columns Time/Date,
    /// Open, High, Low and Close are matched by name (case-insensitive). When the
    /// file has no header the positional layout is assumed:
    /// time,open,high,low,close[,volume].
    /// </summary>
    public static List<CandleResponseDto> LoadCandles(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Data file not found.", path);

        var lines = File.ReadAllLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count == 0)
            throw new InvalidDataException("The data file is empty.");

        // Strip the UTF-8 BOM that often prefixes the first line of an export.
        if (lines[0].StartsWith('\uFEFF'))
            lines[0] = lines[0][1..];

        bool hasHeader = lines[0].Any(ch => char.IsLetter(ch));

        int timeIdx = -1;
        int dateIdx = -1;
        int openIdx = -1;
        int highIdx = -1;
        int lowIdx = -1;
        int closeIdx = -1;

        if (hasHeader)
        {
            var header = SplitCsv(lines[0]);
            for (int i = 0; i < header.Count; i++)
            {
                switch (header[i].Trim().ToLowerInvariant())
                {
                    case "time":
                    case "datetime":
                    case "timestamp":
                        timeIdx = i;
                        break;
                    case "date":
                        dateIdx = i;
                        break;
                    case "open":
                    case "o":
                        openIdx = i;
                        break;
                    case "high":
                    case "h":
                        highIdx = i;
                        break;
                    case "low":
                    case "l":
                        lowIdx = i;
                        break;
                    case "close":
                    case "c":
                        closeIdx = i;
                        break;
                }
            }
        }

        if (timeIdx < 0)
            timeIdx = 0;
        if (openIdx < 0)
            openIdx = hasHeader ? -1 : 1;
        if (highIdx < 0)
            highIdx = hasHeader ? -1 : 2;
        if (lowIdx < 0)
            lowIdx = hasHeader ? -1 : 3;
        if (closeIdx < 0)
            closeIdx = hasHeader ? -1 : 4;

        if (openIdx < 0 || highIdx < 0 || lowIdx < 0 || closeIdx < 0)
        {
            throw new InvalidDataException(
                "The CSV must contain Open, High, Low and Close columns " +
                "(optionally a Time/Date column).");
        }

        var candles = new List<CandleResponseDto>();
        int start = hasHeader ? 1 : 0;

        for (int i = start; i < lines.Count; i++)
        {
            var parts = SplitCsv(lines[i]);
            int maxIndex = Math.Max(timeIdx, Math.Max(openIdx, Math.Max(highIdx, Math.Max(lowIdx, closeIdx))));
            if (parts.Count <= maxIndex)
                continue;

            var candle = new CandleResponseDto
            {
                Open = ParseDouble(parts[openIdx]),
                High = ParseDouble(parts[highIdx]),
                Low = ParseDouble(parts[lowIdx]),
                Close = ParseDouble(parts[closeIdx]),
            };

            candle.Time = ParseTime(parts, timeIdx, dateIdx);

            if (candle.Open > 0 && candle.High > 0 && candle.Low > 0 && candle.Close > 0)
                candles.Add(candle);
        }

        if (candles.Count == 0)
            throw new InvalidDataException("No valid OHLC rows could be parsed.");

        return candles
            .OrderBy(c => c.Time)
            .ToList();
    }

    /// <summary>
    /// Loads OHLC candles from a CSV file asynchronously.
    /// </summary>
    public static async Task<List<CandleResponseDto>> LoadCandlesAsync(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Data file not found.", path);

        var lines = await File.ReadAllLinesAsync(path);
        var filteredLines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        if (filteredLines.Count == 0)
            throw new InvalidDataException("The data file is empty.");

        if (filteredLines[0].StartsWith('\uFEFF'))
            filteredLines[0] = filteredLines[0][1..];

        bool hasHeader = filteredLines[0].Any(ch => char.IsLetter(ch));

        int timeIdx = -1;
        int dateIdx = -1;
        int openIdx = -1;
        int highIdx = -1;
        int lowIdx = -1;
        int closeIdx = -1;

        if (hasHeader)
        {
            var header = SplitCsv(filteredLines[0]);
            for (int i = 0; i < header.Count; i++)
            {
                switch (header[i].Trim().ToLowerInvariant())
                {
                    case "time":
                    case "datetime":
                    case "timestamp":
                        timeIdx = i;
                        break;
                    case "date":
                        dateIdx = i;
                        break;
                    case "open":
                    case "o":
                        openIdx = i;
                        break;
                    case "high":
                    case "h":
                        highIdx = i;
                        break;
                    case "low":
                    case "l":
                        lowIdx = i;
                        break;
                    case "close":
                    case "c":
                        closeIdx = i;
                        break;
                }
            }
        }

        if (timeIdx < 0)
            timeIdx = 0;
        if (openIdx < 0)
            openIdx = hasHeader ? -1 : 1;
        if (highIdx < 0)
            highIdx = hasHeader ? -1 : 2;
        if (lowIdx < 0)
            lowIdx = hasHeader ? -1 : 3;
        if (closeIdx < 0)
            closeIdx = hasHeader ? -1 : 4;

        if (openIdx < 0 || highIdx < 0 || lowIdx < 0 || closeIdx < 0)
        {
            throw new InvalidDataException(
                "The CSV must contain Open, High, Low and Close columns " +
                "(optionally a Time/Date column).");
        }

        var candles = new List<CandleResponseDto>();
        int start = hasHeader ? 1 : 0;

        for (int i = start; i < filteredLines.Count; i++)
        {
            var parts = SplitCsv(filteredLines[i]);
            int maxIndex = Math.Max(timeIdx, Math.Max(openIdx, Math.Max(highIdx, Math.Max(lowIdx, closeIdx))));
            if (parts.Count <= maxIndex)
                continue;

            var candle = new CandleResponseDto
            {
                Open = ParseDouble(parts[openIdx]),
                High = ParseDouble(parts[highIdx]),
                Low = ParseDouble(parts[lowIdx]),
                Close = ParseDouble(parts[closeIdx]),
            };

            candle.Time = ParseTime(parts, timeIdx, dateIdx);

            if (candle.Open > 0 && candle.High > 0 && candle.Low > 0 && candle.Close > 0)
                candles.Add(candle);
        }

        if (candles.Count == 0)
            throw new InvalidDataException("No valid OHLC rows could be parsed.");

        return candles
            .OrderBy(c => c.Time)
            .ToList();
    }

    private static List<string> SplitCsv(string line)
        => line.Split(',', StringSplitOptions.TrimEntries).ToList();

    private static double ParseDouble(string value)
    {
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d;

        return double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out d)
            ? d
            : 0;
    }

    private static DateTime ParseTime(List<string> parts, int timeIdx, int dateIdx)
    {
        string raw = parts[timeIdx].Trim();

        if (dateIdx >= 0 && dateIdx < parts.Count)
            raw = $"{parts[dateIdx].Trim()} {raw}".Trim();

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;

        if (DateTime.TryParseExact(raw, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            return parsed;

        return DateTime.MinValue;
    }

    #endregion

    #region Runner

    /// <summary>
    /// Feeds the given candles into a fresh <see cref="ManualStrategy"/> one by
    /// one and resolves every opened trade against the following candles.
    /// </summary>
    public static Task<BackTestResult> RunAsync(
        string symbol,
        List<CandleResponseDto> candles,
        double startingBalance = 10_000,
        double spread = DefaultSpread,
        SymbolInfoResponseDto? symbolInfo = null)
    {
        return Task.Run(() => Run(symbol, candles, startingBalance, spread, symbolInfo));
    }

    /// <summary>
    /// Feeds the given candles into a fresh <see cref="ManualStrategy"/> one by
    /// one and resolves every opened trade against the following candles.
    /// </summary>
    public static BackTestResult Run(
        string symbol,
        List<CandleResponseDto> candles,
        double startingBalance = 10_000,
        double spread = DefaultSpread,
        SymbolInfoResponseDto? symbolInfo = null)
    {
        var started = DateTime.UtcNow;

        var account = new BackTestAccountService { Balance = startingBalance };
        var trading = new BackTestTradingService();
        if (symbolInfo is not null)
            trading.SymbolInfo = symbolInfo;
        var strategy = new ManualStrategy(trading, account, symbol);

        var openTrades = new List<BackTestTrade>();
        var capturedIds = new HashSet<int>();
        var result = new BackTestResult
        {
            Symbol = symbol,
            Currency = account.Currency,
            CandleCount = candles.Count,
            StartingBalance = startingBalance,
            StartedAt = started,
        };

        // Capture every decision the strategy makes, candle by candle.
        strategy.DecisionLogged += d => result.Decisions.Add(d);

        double peak = startingBalance;
        double maxDrawdown = 0;

        void UpdateDrawdown()
        {
            peak = Math.Max(peak, account.Balance);
            maxDrawdown = Math.Max(maxDrawdown, peak - account.Balance);
        }

        for (int i = 0; i < candles.Count; i++)
        {
            var candle = candles[i];

            // Close the previous forming candle inside the strategy, then treat
            // the current candle as the forming one.
            strategy.Feed(ToUpdate(candle, symbol));

            // Resolve SL/TP of any open trades against the forming candle's range.
            ResolveOpenTrades(candle, openTrades, account, trading.SymbolInfo, UpdateDrawdown);

            // Simulate ticks that touch the entry level, if the strategy is
            // waiting for one.
            SimulateEntry(symbol, candle, strategy, trading, spread, result);

            // Capture any trade that just opened so its OpenTime matches the
            // candle that triggered it.
            foreach (var trade in trading.Trades)
            {
                if (capturedIds.Add(trade.Id))
                {
                    trade.OpenTime = candle.Time;
                    openTrades.Add(trade);
                }
            }
        }

        // The last candle was only a forming candle - close it structurally too.
        if (candles.Count > 0)
            strategy.CheckForTrend(candles[^1]);

        // Close any trade still open at the end of the data at the final close.
        if (candles.Count > 0)
        {
            var last = candles[^1];
            foreach (var trade in openTrades.ToList())
            {
                trade.CloseTime = last.Time;
                trade.ExitPrice = last.Close;
                trade.ExitReason = "EndOfData";
                trade.Profit = ComputeProfit(trade, trading.SymbolInfo);
                account.Balance += trade.Profit;
                UpdateDrawdown();
                openTrades.Remove(trade);
            }
        }

        result.Trades = trading.Trades;
        result.EndingBalance = account.Balance;
        result.MaxDrawdown = maxDrawdown;
        result.Duration = DateTime.UtcNow - started;

        return result;
    }

    private static void SimulateEntry(
        string symbol,
        CandleResponseDto candle,
        ManualStrategy strategy,
        BackTestTradingService trading,
        double spread,
        BackTestResult result)
    {
        if (strategy.CurrentState != ManualStrategy.StrategyState.WaitingForEntry)
            return;

        var entry = strategy.Entry;
        var trend = strategy.CurrentTrend;

        if (trend == ManualStrategy.TrendMode.UpWard)
        {
            // SELL: the candle's body or shadow must collide with the ENTRY
            // point itself (the blue dot). The entry price must be inside the
            // candle's range [Low..High]; a candle that only pokes its wick up
            // to the entry (or gaps above it) is NOT enough.
            bool reached = candle.Low <= entry && entry <= candle.High;
            bool body = BodyTouches(candle, entry);

            LogEntryCheck(result, candle, "SELL", entry, body, reached);

            if (reached)
            {
                trading.CurrentBid = entry;
                trading.CurrentAsk = entry + spread;
                strategy.OnTick(new TickUpdateDto
                {
                    Symbol = symbol,
                    Bid = entry,
                    Ask = entry + spread,
                    Time = candle.Time,
                });
            }
        }
        else if (trend == ManualStrategy.TrendMode.DownWard)
        {
            // BUY: the candle's body or shadow must collide with the ENTRY
            // point itself (the blue dot). The entry price must be inside the
            // candle's range [Low..High]. Candles are bid-based, so the tick is
            // built as Bid = entry - spread / Ask = entry; the strategy's
            // OnTick (Ask <= entry) then triggers and fills at the entry.
            bool reached = candle.Low <= entry && entry <= candle.High;
            bool body = BodyTouches(candle, entry);

            LogEntryCheck(result, candle, "BUY", entry, body, reached);

            if (reached)
            {
                trading.CurrentBid = entry - spread;
                trading.CurrentAsk = entry;
                strategy.OnTick(new TickUpdateDto
                {
                    Symbol = symbol,
                    Bid = entry - spread,
                    Ask = entry,
                    Time = candle.Time,
                });
            }
        }
    }

    /// <summary>
    /// True when the entry level lies inside the candle's body
    /// (Open..Close), i.e. the BODY collides with the entry point.
    /// </summary>
    private static bool BodyTouches(
        CandleResponseDto candle,
        double level)
    {
        double lo = Math.Min(candle.Open, candle.Close);
        double hi = Math.Max(candle.Open, candle.Close);
        return level >= lo && level <= hi;
    }

    private static void LogEntryCheck(
        BackTestResult result,
        CandleResponseDto candle,
        string side,
        double entry,
        bool body,
        bool reached)
    {
        result.Decisions.Add(new StrategyDecision
        {
            Time = candle.Time,
            State = ManualStrategy.StrategyState.WaitingForEntry,
            Trend = side == "SELL"
                ? ManualStrategy.TrendMode.UpWard
                : ManualStrategy.TrendMode.DownWard,
            Entry = entry,
            Open = candle.Open,
            High = candle.High,
            Low = candle.Low,
            Close = candle.Close,
            Decision = reached
                ? (body
                    ? $"ENTRY REACHED by {side} BODY (candle body covers entry={entry:F5})"
                    : $"ENTRY REACHED by {side} SHADOW (candle wick covers entry={entry:F5})")
                : $"entry NOT reached by {side} (body/shadow did not cover entry={entry:F5})",
        });
    }

    private static void ResolveOpenTrades(
        CandleResponseDto candle,
        List<BackTestTrade> openTrades,
        BackTestAccountService account,
        SymbolInfoResponseDto symbolInfo,
        Action updateDrawdown)
    {
        if (openTrades.Count == 0)
            return;

        foreach (var trade in openTrades.ToList())
        {
            bool slHit;
            bool tpHit;

            if (trade.Sell)
            {
                slHit = candle.High >= trade.StopLoss;
                tpHit = candle.Low <= trade.TakeProfit;
            }
            else
            {
                slHit = candle.Low <= trade.StopLoss;
                tpHit = candle.High >= trade.TakeProfit;
            }

            if (!slHit && !tpHit)
                continue;

            // When both levels are reached inside one candle the one closest to
            // the open is assumed to have been touched first.
            if (slHit && tpHit)
            {
                double slDist = Math.Abs(candle.Open - trade.StopLoss);
                double tpDist = Math.Abs(candle.Open - trade.TakeProfit);

                if (slDist <= tpDist)
                {
                    trade.ExitPrice = trade.StopLoss;
                    trade.ExitReason = "SL";
                }
                else
                {
                    trade.ExitPrice = trade.TakeProfit;
                    trade.ExitReason = "TP";
                }
            }
            else if (slHit)
            {
                trade.ExitPrice = trade.StopLoss;
                trade.ExitReason = "SL";
            }
            else
            {
                trade.ExitPrice = trade.TakeProfit;
                trade.ExitReason = "TP";
            }

            trade.CloseTime = candle.Time;
            trade.Profit = ComputeProfit(trade, symbolInfo);

            account.Balance += trade.Profit;
            updateDrawdown();

            openTrades.Remove(trade);
        }
    }

    private static double ComputeProfit(BackTestTrade trade, SymbolInfoResponseDto symbolInfo)
    {
        double delta = trade.Sell
            ? trade.EntryPrice - trade.ExitPrice
            : trade.ExitPrice - trade.EntryPrice;

        return delta * symbolInfo.ContractSize * trade.Volume;
    }

    private static CandleUpdateDto ToUpdate(CandleResponseDto candle, string symbol)
        => new()
        {
            Symbol = symbol,
            Timeframe = string.Empty,
            Time = candle.Time,
            Open = candle.Open,
            High = candle.High,
            Low = candle.Low,
            Close = candle.Close,
            TickVolume = candle.TickVolume,
        };

    #endregion

    #region Output

    /// <summary>
    /// Writes a human readable backtest report into
    /// <c>TradingBot.UI/results</c> and returns the full file path.
    /// </summary>
    public static async Task<string> SaveResultsAsync(BackTestResult result, string symbol)
    {
        var directory = FindResultsDirectory();
        Directory.CreateDirectory(directory);

        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string safeSymbol = string.Join("_", symbol.Split(Path.GetInvalidFileNameChars()));
        string path = Path.Combine(directory, $"backtest_{safeSymbol}_{stamp}.txt");

        await File.WriteAllTextAsync(path, BuildReport(result), Encoding.UTF8);

        return path;
    }

    /// <summary>
    /// Writes a human readable backtest report into
    /// <c>TradingBot.UI/results</c> and returns the full file path (synchronous version).
    /// </summary>
    public static string SaveResults(BackTestResult result, string symbol)
    {
        var directory = FindResultsDirectory();
        Directory.CreateDirectory(directory);

        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string safeSymbol = string.Join("_", symbol.Split(Path.GetInvalidFileNameChars()));
        string path = Path.Combine(directory, $"backtest_{safeSymbol}_{stamp}.txt");

        File.WriteAllText(path, BuildReport(result), Encoding.UTF8);

        return path;
    }

    public static string FindResultsDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null &&
               !string.Equals(current.Name, "TradingBot.UI", StringComparison.OrdinalIgnoreCase))
        {
            current = current.Parent;
        }

        return current is not null
            ? Path.Combine(current.FullName, "results")
            : Path.Combine(AppContext.BaseDirectory, "results");
    }

    private static string BuildReport(BackTestResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine("==============================================");
        sb.AppendLine("          BACKTEST REPORT");
        sb.AppendLine("==============================================");
        sb.AppendLine($"Symbol            : {result.Symbol}");
        sb.AppendLine($"Run started       : {result.StartedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Duration          : {result.Duration.TotalSeconds:F1} s");
        sb.AppendLine($"Candles           : {result.CandleCount}");
        sb.AppendLine($"Starting balance  : {result.StartingBalance:F2} {result.Currency}");
        sb.AppendLine($"Ending balance    : {result.EndingBalance:F2} {result.Currency}");
        sb.AppendLine($"Net profit        : {result.NetProfit:+0.00;-0.00;0.00} {result.Currency}");
        sb.AppendLine($"Total trades      : {result.TotalTrades}");
        sb.AppendLine($"Wins / Losses     : {result.Wins} / {result.Losses}");
        sb.AppendLine($"Win rate          : {result.WinRate:P1}");
        sb.AppendLine($"Gross profit      : {result.GrossProfit:F2} {result.Currency}");
        sb.AppendLine($"Gross loss        : {result.GrossLoss:F2} {result.Currency}");
        sb.AppendLine($"Profit factor     : {result.ProfitFactor:F2}");
        sb.AppendLine($"Max drawdown      : {result.MaxDrawdown:F2} {result.Currency}");
        sb.AppendLine("----------------------------------------------");
        sb.AppendLine($"{"#",4}  {"Open time",18}  {"Side",5}  {"Entry",10}  {"SL",10}  {"TP",10}  {"Exit",10}  {"Close time",18}  {"Reason",9}  {"Volume",8}  {"Profit",12}");
        sb.AppendLine("----------------------------------------------");

        for (int i = 0; i < result.Trades.Count; i++)
        {
            var t = result.Trades[i];
            sb.AppendLine(
                $"{t.Id,4}  {t.OpenTime:yyyy-MM-dd HH:mm:ss}  {t.Side,5}  " +
                $"{t.EntryPrice,10:F5}  {t.StopLoss,10:F5}  {t.TakeProfit,10:F5}  " +
                $"{t.ExitPrice,10:F5}  {t.CloseTime:yyyy-MM-dd HH:mm:ss}  " +
                $"{t.ExitReason,9}  {t.Volume,8:F2}  {t.Profit,12:F2}");
        }

        sb.AppendLine("----------------------------------------------");
        sb.AppendLine("Notes:");
        sb.AppendLine("  - Entry: while waiting for the entry price (max 2 candles)");
        sb.AppendLine("    the strategy enters ONLY when one of the waiting candles'");
        sb.AppendLine("    BODY or SHADOW actually covers the entry point (the blue dot):");
        sb.AppendLine("    the entry price must be inside the candle's range [Low..High].");
        sb.AppendLine("    SELL -> the candle must rise so its range covers the entry.");
        sb.AppendLine("    BUY  -> the candle must fall so its range covers the entry.");
        sb.AppendLine("  - SL/TP are resolved against the High/Low of the candles that");
        sb.AppendLine("    follow the entry candle.");
        sb.AppendLine("  - Trades still open at the end of the data are closed at the");
        sb.AppendLine("    last candle close and marked EndOfData.");

        sb.AppendLine("==============================================");
        sb.AppendLine("          PER-CANDLE DECISION LOG");
        sb.AppendLine("==============================================");
        sb.AppendLine($"{"Time",19}  {"State",16}  {"Trend",8}  {"Rev",4}  {"Entry",10}  {"O",9}  {"H",9}  {"L",9}  {"C",9}  Decision");
        sb.AppendLine("----------------------------------------------");

        foreach (var d in result.Decisions)
        {
            sb.AppendLine(
                $"{d.Time:yyyy-MM-dd HH:mm:ss}  {d.State,16}  " +
                $"{d.Trend,8}  {d.ReversalCount,4}  {d.Entry,10:F5}  " +
                $"{d.Open,9:F5}  {d.High,9:F5}  {d.Low,9:F5}  {d.Close,9:F5}  {d.Decision}");
        }

        sb.AppendLine("==============================================");

        return sb.ToString();
    }

    #endregion
}