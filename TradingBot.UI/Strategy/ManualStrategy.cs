using System.Diagnostics;
using Trading.Core.Interfaces;
using Trading.Shared.Events;
using Trading.Shared.Requests;
using Trading.Shared.Responses;

namespace TradingBot.UI.Strategy;

/// <summary>
/// Manual / discretionary strategy.
///
/// Structural rules:
///
/// TREND
/// -----
/// A trend is a sequence of valid candles of the same direction.
/// Every valid continuation must break/touch the relevant level of the
/// previous trend candle without breaking/touching the opposite level.
///
/// REVERSAL
/// --------
/// Once the trend is confirmed:
///
/// Last Trend Candle
///        │
///        ├── R1
///        │    ├── does not break level -> R2 gets the second opportunity
///        │    │
///        │    └── breaks level
///        │          ├── Body -> Body Entry
///        │          └── Shadow -> Shadow Entry
///        │
///        └── R2
///             ├── does not break level -> R1 + R2 become next Trend
///             │
///             └── breaks level
///                   ├── Body -> Body Entry
///                   └── Shadow -> Shadow Entry
///
/// IMPORTANT:
/// R1 and R2 are BOTH compared against the SAME original last Trend candle.
/// R2 is NOT compared against R1.
///
/// The candle that actually breaks the original Trend level determines
/// Body/Shadow. Therefore either R1 or R2 can determine the reversal type.
///
/// REVERSAL DATA PRESERVATION
/// --------------------------
/// Reversal candles are structural data.
///
/// They must not be lost:
/// - if both reversal attempts fail -> R1/R2 become the next Trend.
/// - if reversal succeeds and Entry is reached -> after the trade,
///   R1/R2 are still promoted into the next Trend.
/// - if Entry expires without being reached -> R1/R2 are promoted into
///   the next Trend.
///
/// ENTRY
/// -----
/// Entry range is calculated ONLY from the original Trend structure.
/// Reversal candles are never used to construct the original Trend range.
///
/// UP trend:
///   Body   -> last one-third of trend range
///   Shadow -> middle of trend range
///
/// DOWN trend:
///   Body   -> first one-third of trend range
///   Shadow -> middle of trend range
///
/// STOP LOSS
/// ---------
/// UP trend / SELL:
///   structural HIGH of original trend.
///
/// DOWN trend / BUY:
///   structural LOW of original trend.
///
/// RISK
/// ----
/// Maximum expected loss ~= $100.
/// TP = SL distance * 2.2.
///
/// Live ticks trigger the actual entry.
/// Closed candles drive structural analysis.
/// </summary>
public class ManualStrategy
{
    public const int DefaultMinTrendCandles = 3;

    // The strategy requires two opportunities:
    // R1 and R2.
    public const int DefaultMinReversalCandles = 1;

    //private const double MaxLossUsd = 100.0;
    private const double RiskPercentPerTrade = 0.005; // 0.5% of account balance
    private const double RiskRewardRatio = 2.2;

    private readonly ITradingService _trading;
    private readonly IAccountService _account;
    private readonly string _symbol;

    private readonly int _minTrendCandles;
    private readonly int _minReversalCandles;

    private readonly object _gate = new();

    /// <summary>
    /// Valid trend candles only.
    /// </summary>
    private readonly List<CandleResponseDto> _trend = [];

    /// <summary>
    /// Reversal opportunity candles.
    ///
    /// R1 and R2 are kept here until they are either:
    /// - converted to the next trend, or
    /// - used for a successful setup and later promoted after trade.
    /// </summary>
    private readonly List<CandleResponseDto> _reversals = [];

    /// <summary>
    /// Candles counted while waiting for the Entry price.
    /// These candles DO NOT modify the setup.
    /// </summary>
    private readonly List<CandleResponseDto> _waitedForEntryPoint = [];

    private readonly SemaphoreSlim _tradeSerial = new(1, 1);

    private CandleResponseDto? _forming;

    private TrendMode _modeOfTrend = TrendMode.None;
    private ReversMode _modeOfRevers = ReversMode.None;

    private int _countOfRevers;

    private double _entry;

    /// <summary>
    /// The exact Trend candle against which BOTH R1 and R2 are checked.
    ///
    /// This is captured when R1 appears and never changes during the
    /// two-reversal opportunity window.
    /// </summary>
    private CandleResponseDto? _lastReversalTrend;

    /// <summary>
    /// Structural Stop Loss boundary from the ORIGINAL trend.
    /// </summary>
    private double _structuralSl;

    private DateTime _lastTickLogUtc;

    private StrategyState _state = StrategyState.WaitingForTrend;

    /// <summary>
    /// Maximum number of closed candles allowed while waiting for Entry.
    /// </summary>
    private readonly int _maxWaitForEntryPoint = 2;

    private readonly List<CandleResponseDto> _pendingDuringExecution = [];

    public ManualStrategy(
        ITradingService trading,
        IAccountService account,
        string symbol,
        int minTrendCandles = DefaultMinTrendCandles,
        int minReversalCandles = DefaultMinReversalCandles)
    {
        _trading = trading ?? throw new ArgumentNullException(nameof(trading));
        _account = account ?? throw new ArgumentNullException(nameof(account));
        _symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));

        _minTrendCandles = Math.Max(1, minTrendCandles);
        _minReversalCandles = Math.Max(1, minReversalCandles);
    }

    #region Feed

    public void Feed(CandleUpdateDto candle)
    {
        if (candle == null ||
            !string.Equals(
                candle.Symbol,
                _symbol,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var dto = new CandleResponseDto
        {
            Time = candle.Time,
            Open = candle.Open,
            High = candle.High,
            Low = candle.Low,
            Close = candle.Close,
            TickVolume = candle.TickVolume
        };

        lock (_gate)
        {
            // The previous forming candle is now closed.
            if (_forming is not null &&
                _forming.Time != dto.Time)
            {
                Log(
                    $"Feed: closed candle {_forming.Time:HH:mm:ss} " +
                    $"(O={_forming.Open}, H={_forming.High}, " +
                    $"L={_forming.Low}, C={_forming.Close}) -> processing.");

                ProcessCandle(_forming);
            }

            _forming = dto;
        }
    }

    #endregion

    #region Tick / Entry

    public void OnTick(TickUpdateDto tick)
    {
        if (tick == null ||
            !string.Equals(
                tick.Symbol,
                _symbol,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        bool sell = false;
        bool shouldTrade = false;
        double executionPrice = 0;

        lock (_gate)
        {
            if (_state != StrategyState.WaitingForEntry)
                return;

            if (_modeOfTrend == TrendMode.UpWard)
            {
                // SELL -> Bid reaches Entry.
                shouldTrade = tick.Bid >= _entry;
            }
            else if (_modeOfTrend == TrendMode.DownWard)
            {
                // BUY -> Ask reaches Entry.
                shouldTrade = tick.Ask <= _entry;
            }
            else
            {
                return;
            }

            var now = DateTime.UtcNow;

            if ((now - _lastTickLogUtc).TotalSeconds >= 2.0)
            {
                _lastTickLogUtc = now;

                Log(
                    $"WaitingForEntry: state={_state}, " +
                    $"trend={_modeOfTrend}, " +
                    $"entry={_entry:F5}, " +
                    $"bid={tick.Bid:F5}, " +
                    $"ask={tick.Ask:F5}, " +
                    $"entryReached={shouldTrade}");
            }

            if (!shouldTrade)
                return;

            sell = _modeOfTrend == TrendMode.UpWard;

            executionPrice = sell
                ? tick.Bid
                : tick.Ask;

            _state = StrategyState.ExecutingTrade;

            Log(
                $"Entry reached ({(sell ? "SELL" : "BUY")}): " +
                $"entry={_entry:F5}, " +
                $"executionPrice={executionPrice:F5}, " +
                $"bid={tick.Bid:F5}, ask={tick.Ask:F5}.");
        }

        if (shouldTrade)
            RunTradeAsync(sell, executionPrice);
    }

    #endregion

    #region Candle Processing

    public void CheckForTrend(CandleResponseDto candle)
    {
        if (candle == null)
            return;

        ProcessCandle(candle);
    }

    private void ProcessCandle(CandleResponseDto candle)
    {
        lock (_gate)
        {
            if (candle == null)
                return;

            // Trade is currently being submitted.
            // The setup is sealed.
            if (_state == StrategyState.ExecutingTrade)
            {
                _pendingDuringExecution.Add(candle);

                Log(
                    $"Candle {candle.Time:HH:mm:ss} queued while ExecutingTrade " +
                    $"(pending={_pendingDuringExecution.Count}).");
                return;
            }


            // Entry is tick-driven.
            // Candles only count the waiting window.
            if (_state == StrategyState.WaitingForEntry)
            {
                _waitedForEntryPoint.Add(candle);

                Log(
                    $"WaitingForEntry candle {_waitedForEntryPoint.Count}/" +
                    $"{_maxWaitForEntryPoint}: {candle.Time:HH:mm:ss}");

                if (_waitedForEntryPoint.Count >= _maxWaitForEntryPoint)
                {
                    Log(
                        $"Entry was not reached within " +
                        $"{_maxWaitForEntryPoint} candles. " +
                        $"Promoting reversal candles to next trend.");

                    ConvertReversalToTrend();
                }

                return;
            }

            // No trend yet.
            if (_trend.Count == 0)
            {
                SeedTrend(candle);
                return;
            }

            // Trend is still being built.
            if (_trend.Count < _minTrendCandles)
            {
                ProcessTrendBuilding(candle);
                return;
            }

            // Confirmed trend.
            ProcessReversalPhase(candle);
        }
    }

    #endregion

    #region Basic Candle Structure

    private static bool IsBullish(CandleResponseDto candle)
        => candle.Close > candle.Open;

    private static bool IsBearish(CandleResponseDto candle)
        => candle.Close < candle.Open;

    /// <summary>
    /// Noise:
    /// - breaks neither side
    /// - breaks both sides
    /// - custom opposite-direction structural noise
    /// - body is <= 10% of total range
    /// </summary>
    private static bool IsNoise(
        CandleResponseDto candle,
        CandleResponseDto prev)
    {
        bool breaksHigh = candle.High >= prev.High;
        bool breaksLow = candle.Low <= prev.Low;

        // Custom noise rules.
        if (prev.Open < prev.Close &&
            candle.High > prev.High &&
            candle.Low > prev.Low &&
            candle.Open > candle.Close)
        {
            return true;
        }

        if (prev.Open > prev.Close &&
            candle.Low < prev.Low &&
            candle.High < prev.High &&
            candle.Open < candle.Close)
        {
            return true;
        }

        double bodySize = Math.Abs(candle.Close - candle.Open);
        double totalRange = candle.High - candle.Low;

        if (totalRange > 0)
        {
            double bodyToRangeRatio = bodySize / totalRange;

            if (bodyToRangeRatio <= 0.10)
                return true;
        }

        return (!breaksHigh && !breaksLow) ||
               (breaksHigh && breaksLow);
    }

    private static bool IsValidUpward(
        CandleResponseDto candle,
        CandleResponseDto prev)
        => IsBullish(candle) &&
           candle.High >= prev.High &&
           candle.Low > prev.Low;

    private static bool IsValidDownward(
        CandleResponseDto candle,
        CandleResponseDto prev)
        => IsBearish(candle) &&
           candle.Low <= prev.Low &&
           candle.High < prev.High;

    private bool IsValidTrendContinuation(
        CandleResponseDto candle,
        CandleResponseDto lastTrend)
    {
        return _modeOfTrend switch
        {
            TrendMode.UpWard =>
                IsValidUpward(candle, lastTrend),

            TrendMode.DownWard =>
                IsValidDownward(candle, lastTrend),

            _ => false
        };
    }

    #endregion

    #region Trend Building

    private void SeedTrend(CandleResponseDto candle)
    {
        _trend.Clear();
        _reversals.Clear();

        _countOfRevers = 0;
        _modeOfRevers = ReversMode.None;

        _entry = 0;
        _structuralSl = 0;

        _lastReversalTrend = null;

        if (IsBullish(candle))
        {
            _modeOfTrend = TrendMode.UpWard;
            _trend.Add(candle);
        }
        else if (IsBearish(candle))
        {
            _modeOfTrend = TrendMode.DownWard;
            _trend.Add(candle);
        }
        else
        {
            _modeOfTrend = TrendMode.None;
        }

        UpdateTrendState();

        Log(
            $"SeedTrend: candle={candle.Time:HH:mm:ss}, " +
            $"trend={_modeOfTrend}, " +
            $"trendCandles={_trend.Count}, " +
            $"state={_state}.");
    }

    private void ProcessTrendBuilding(CandleResponseDto candle)
    {
        var lastTrend = _trend[^1];

        if (_modeOfTrend == TrendMode.UpWard &&
            IsValidUpward(candle, lastTrend))
        {
            _trend.Add(candle);
        }
        else if (_modeOfTrend == TrendMode.DownWard &&
                 IsValidDownward(candle, lastTrend))
        {
            _trend.Add(candle);
        }
        else if (IsNoise(candle, lastTrend))
        {
            Log(
                $"Noise candle {candle.Time:HH:mm:ss} skipped. " +
                $"trend={_modeOfTrend}, " +
                $"trendCandles={_trend.Count}/{_minTrendCandles}.");

            return;
        }
        else
        {
            Log(
                $"Trend {_modeOfTrend} broken at " +
                $"{candle.Time:HH:mm:ss}. Reseeding trend.");

            SeedTrend(candle);
            return;
        }

        UpdateTrendState();

        Log(
            $"Trend {_modeOfTrend}: added candle " +
            $"{candle.Time:HH:mm:ss} -> " +
            $"trendCandles={_trend.Count}/{_minTrendCandles}, " +
            $"state={_state}.");
    }

    private void UpdateTrendState()
    {
        _state = _trend.Count >= _minTrendCandles
            ? StrategyState.WaitingForReversal
            : _modeOfTrend == TrendMode.UpWard
                ? StrategyState.UpTrend
                : _modeOfTrend == TrendMode.DownWard
                    ? StrategyState.DownTrend
                    : StrategyState.WaitingForTrend;
    }

    #endregion

    #region Reversal

    /// <summary>
    /// Main reversal state machine.
    ///
    /// R1 and R2 are BOTH checked against _lastReversalTrend.
    /// </summary>
    private void ProcessReversalPhase(CandleResponseDto candle)
    {
        // ============================================================
        // R1
        // ============================================================

        if (_countOfRevers == 0)
        {
            if (IsReversalDirection(candle))
            {
                // Capture the original reference candle exactly once.
                _lastReversalTrend = _trend[^1];

                _reversals.Add(candle);
                _countOfRevers = 1;

                Log(
                    $"R1 recorded: {candle.Time:HH:mm:ss}, " +
                    $"reference Trend candle={_lastReversalTrend.Time:HH:mm:ss}, " +
                    $"reference H={_lastReversalTrend.High}, " +
                    $"L={_lastReversalTrend.Low}.");

                // R1 is checked against LAST TREND.
                if (BreaksLastTrendLevel(candle, _lastReversalTrend))
                {
                    ConfirmReversal(candle, _lastReversalTrend);

                    Log(
                        $"REVERSAL CONFIRMED BY R1: " +
                        $"type={_modeOfRevers}, " +
                        $"entry={_entry:F5}, " +
                        $"state={_state}.");
                }
                else
                {
                    Log(
                        $"R1 did NOT break the original Trend level. " +
                        $"R2 gets the second opportunity.");
                }

                return;
            }

            // Reversal has not started.
            // Trend can continue normally.
            if (IsValidTrendContinuation(candle, _trend[^1]))
            {
                _trend.Add(candle);

                Log(
                    $"Trend {_modeOfTrend} extended: " +
                    $"{candle.Time:HH:mm:ss} -> " +
                    $"trendCandles={_trend.Count}.");
            }

            return;
        }

        // ============================================================
        // R2
        // ============================================================

        if (_countOfRevers == 1)
        {
            // IMPORTANT:
            // R2 uses the SAME reference candle captured by R1.
            var referenceTrend =
                _lastReversalTrend ?? _trend[^1];

            if (IsReversalDirection(candle))
            {
                _reversals.Add(candle);
                _countOfRevers = 2;

                Log(
                    $"R2 recorded: {candle.Time:HH:mm:ss}, " +
                    $"reference Trend candle={referenceTrend.Time:HH:mm:ss}, " +
                    $"reference H={referenceTrend.High}, " +
                    $"L={referenceTrend.Low}.");

                // R2 is also checked against the SAME LAST TREND.
                if (BreaksLastTrendLevel(candle, referenceTrend))
                {
                    // IMPORTANT:
                    // Body/Shadow comes from R2 because R2 is the candle
                    // that actually broke the level.
                    ConfirmReversal(candle, referenceTrend);

                    Log(
                        $"REVERSAL CONFIRMED BY R2: " +
                        $"type={_modeOfRevers}, " +
                        $"entry={_entry:F5}, " +
                        $"state={_state}.");
                }
                else
                {
                    // Both opportunities failed.
                    // R1 + R2 become the next trend.
                    Log(
                        $"R2 did NOT break the original Trend level. " +
                        $"Both reversal opportunities failed. " +
                        $"R1 + R2 -> next Trend.");

                    ConvertReversalToTrend();
                }

                return;
            }

            // If the candle is not a reversal-direction candle,
            // it does NOT consume the second reversal opportunity.
            //
            // A valid trend continuation keeps the trend alive.
            if (IsValidTrendContinuation(candle, _trend[^1]))
            {
                _trend.Add(candle);

                Log(
                    $"Trend {_modeOfTrend} extended while R1 is pending: " +
                    $"{candle.Time:HH:mm:ss} -> " +
                    $"trendCandles={_trend.Count}, " +
                    $"R1 still pending.");
            }

            return;
        }

        // Safety fallback.
        if (_countOfRevers >= 2)
        {
            ConvertReversalToTrend();
        }
    }

    /// <summary>
    /// Checks only the opposite candle direction.
    ///
    /// This intentionally does NOT check the break level.
    /// Break level is checked separately so that R1/R2 can be opportunities.
    /// </summary>
    private bool IsReversalDirection(CandleResponseDto candle)
    {
        return _modeOfTrend switch
        {
            TrendMode.UpWard => IsBearish(candle),
            TrendMode.DownWard => IsBullish(candle),
            _ => false
        };
    }

    /// <summary>
    /// Checks whether the reversal candle actually broke the ORIGINAL
    /// last Trend candle.
    ///
    /// UpTrend:
    ///   bearish reversal must break/touch Trend Low.
    ///
    /// DownTrend:
    ///   bullish reversal must break/touch Trend High.
    /// </summary>
    private bool BreaksLastTrendLevel(
        CandleResponseDto candle,
        CandleResponseDto lastTrend)
    {
        return _modeOfTrend switch
        {
            TrendMode.UpWard =>
                IsBearish(candle) &&
                candle.Low <= lastTrend.Low,

            TrendMode.DownWard =>
                IsBullish(candle) &&
                candle.High >= lastTrend.High,

            _ => false
        };
    }

    /// <summary>
    /// Determines whether the candle that actually broke the original
    /// Trend level did it with BODY or SHADOW.
    ///
    /// This can be R1 OR R2.
    /// </summary>
    private ReversMode GetBreakType(
        CandleResponseDto breakingCandle,
        CandleResponseDto lastTrend)
    {
        return UsesBodyBreak(breakingCandle, lastTrend)
            ? ReversMode.Body
            : ReversMode.Shadow;
    }

    private bool UsesBodyBreak(
        CandleResponseDto candle,
        CandleResponseDto lastTrend)
    {
        return _modeOfTrend switch
        {
            TrendMode.UpWard =>
                candle.Close < lastTrend.Low,

            TrendMode.DownWard =>
                candle.Close > lastTrend.High,

            _ => false
        };
    }

    /// <summary>
    /// Seals a successful reversal setup.
    ///
    /// breakingCandle can be R1 or R2.
    /// </summary>
    private void ConfirmReversal(
        CandleResponseDto breakingCandle,
        CandleResponseDto lastTrend)
    {
        _modeOfRevers =
            GetBreakType(breakingCandle, lastTrend);

        _entry = CalculateEntry();

        _state = StrategyState.WaitingForEntry;

        _waitedForEntryPoint.Clear();

        Log(
            $"ConfirmReversal: " +
            $"breakingCandle={breakingCandle.Time:HH:mm:ss}, " +
            $"referenceTrend={lastTrend.Time:HH:mm:ss}, " +
            $"reversalType={_modeOfRevers}, " +
            $"entry={_entry:F5}.");
    }

    #endregion

    #region Entry Calculation

    /// <summary>
    /// Calculates Entry from last trend and reversals.
    ///
    /// UP trend (bearish reversal):
    ///   High -> last Trend candle's High (structural reference).
    ///   Low  -> lowest Low among recorded reversal candles (R1/R2).
    ///
    /// DOWN trend (bullish reversal):
    ///   Low  -> last Trend candle's Low (structural reference).
    ///   High -> highest High among recorded reversal candles (R1/R2).
    ///
    /// UP trend / SELL:
    ///   Body   -> 2/3 from range Low toward range High.
    ///   Shadow -> 1/2 from range Low toward range High.
    ///
    /// DOWN trend / BUY:
    ///   Body   -> 1/3 from range Low toward range High.
    ///   Shadow -> 1/2 from range Low toward range High.
    /// </summary>
    private double CalculateEntry()
    {
        if (_trend.Count == 0 || _reversals.Count == 0)
            return 0;

        
        var referenceTrend = _lastReversalTrend ?? _trend[^1];

        double trendLow;
        double trendHigh;
        double range;
        double entry;

        if (_modeOfTrend == TrendMode.UpWard)
        {

            trendHigh = referenceTrend.High;
            trendLow = _reversals.Min(c => c.Low);

            range = trendHigh - trendLow;

            entry = _modeOfRevers == ReversMode.Body
                ? trendLow + (range * 2.0 / 3.0)
                : trendLow + (range / 2.0);

            _structuralSl = referenceTrend.High;
        }
        else if (_modeOfTrend == TrendMode.DownWard)
        {

            trendLow = referenceTrend.Low;
            trendHigh = _reversals.Max(c => c.High);

            range = trendHigh - trendLow;

            entry = _modeOfRevers == ReversMode.Body
                ? trendLow + (range / 3.0)
                : trendLow + (range / 2.0);

            _structuralSl = referenceTrend.Low;
        }
        else
        {
            return 0;
        }

        if (range <= 0)
        {
            Log($"CalculateEntry failed: invalid range. low={trendLow:F5}, high={trendHigh:F5}.");
            return 0;
        }

        Log($"Entry calc: trend={_modeOfTrend}, low={trendLow:F5}, high={trendHigh:F5}, " +
            $"range={range:F5}, type={_modeOfRevers}, entry={entry:F5}, sl={_structuralSl:F5}.");

        return entry;
    }
    #endregion

    #region Reversal -> Trend Conversion

    /// <summary>
    /// Converts currently stored reversal candles into the next Trend.
    ///
    /// Used when:
    /// 1. R1 and R2 both fail.
    /// 2. Entry waiting period expires.
    ///
    /// IMPORTANT:
    /// The reversal data is copied before clearing it.
    /// </summary>
    private void ConvertReversalToTrend()
    {
        var reversTemp = _reversals.ToList();

        if (reversTemp.Count == 0)
        {
            ResetToWaitingForTrend();
            return;
        }

        _trend.Clear();

        foreach (var candle in reversTemp)
            _trend.Add(candle);

        _reversals.Clear();
        _waitedForEntryPoint.Clear();

        _countOfRevers = 0;

        _modeOfRevers = ReversMode.None;

        _entry = 0;
        _structuralSl = 0;

        _lastReversalTrend = null;

        DetermineTrendDirectionFromSeed();

        UpdateTrendState();

        Log(
            $"ConvertReversalToTrend: promoted " +
            $"{reversTemp.Count} reversal candle(s) into next trend. " +
            $"trend={_modeOfTrend}, " +
            $"trendCandles={_trend.Count}, " +
            $"state={_state}.");
    }

    /// <summary>
    /// IMPORTANT:
    /// This is used AFTER a trade too.
    ///
    /// Even if the reversal strategy was successful, R1/R2 are still
    /// valuable structural data for the next Trend.
    ///
    /// Therefore a successful trade does NOT destroy reversal data.
    /// </summary>
    private void PromoteReversalToTrendAfterTrade()
    {
        var reversTemp = _reversals.ToList();

        if (reversTemp.Count == 0)
        {
            ResetToWaitingForTrend();
            return;
        }

        _trend.Clear();

        foreach (var candle in reversTemp)
            _trend.Add(candle);

        _reversals.Clear();
        _waitedForEntryPoint.Clear();

        _countOfRevers = 0;

        _modeOfRevers = ReversMode.None;

        _entry = 0;
        _structuralSl = 0;

        _lastReversalTrend = null;

        DetermineTrendDirectionFromSeed();

        UpdateTrendState();

        Log(
            $"Trade completed: preserved " +
            $"{reversTemp.Count} successful reversal candle(s) " +
            $"as next Trend. " +
            $"trend={_modeOfTrend}, " +
            $"trendCandles={_trend.Count}, " +
            $"state={_state}.");
    }

    private void DetermineTrendDirectionFromSeed()
    {
        if (_trend.Count == 0)
        {
            _modeOfTrend = TrendMode.None;
            return;
        }

        if (IsBullish(_trend[0]))
        {
            _modeOfTrend = TrendMode.UpWard;
        }
        else if (IsBearish(_trend[0]))
        {
            _modeOfTrend = TrendMode.DownWard;
        }
        else
        {
            _modeOfTrend = TrendMode.None;
        }
    }

    #endregion

    #region Trade Execution

    //private const double RiskRewardRatio = 2.2;
    private async void RunTradeAsync(
    bool sell,
    double executionPrice)
    {
        bool acquired = false;

        try
        {
            await _tradeSerial.WaitAsync();

            acquired = true;

            // Account balance drives position sizing (0.5% risk per trade).
            var account =
                await _account.GetAccountInfoAsync();

            if (account.Balance <= 0)
            {
                Log(
                    $"ABORT: invalid account balance={account.Balance:F2}.");

                return;
            }

            // Max loss for this trade = 0.5% of current account balance.
            double maxLossUsd = account.Balance * RiskPercentPerTrade;

            Log(
                $"Account: balance={account.Balance:F2}, " +
                $"riskPercent={RiskPercentPerTrade:P2}, " +
                $"maxRisk={maxLossUsd:F2}, " +
                $"maxReward={maxLossUsd * RiskRewardRatio:F2}.");

            var symbol =
                await _trading.GetSymbolInfoAsync(_symbol);

            double structuralSl;

            lock (_gate)
            {
                structuralSl = _structuralSl;
            }

            // Distance between entry and the structural stop loss.
            double slDistance = sell
                ? structuralSl - executionPrice
                : executionPrice - structuralSl;

            if (slDistance <= 0)
            {
                Log(
                    $"ABORT: invalid structural SL. " +
                    $"structuralSl={structuralSl:F5}, " +
                    $"executionPrice={executionPrice:F5}, " +
                    $"slDistance={slDistance:F5}.");

                return;
            }

            // Monetary loss per 1.0 lot at this SL distance.
            double lossPerLot =
                (slDistance / symbol.TickSize) *
                symbol.TickValue;

            if (lossPerLot <= 0)
            {
                Log(
                    $"ABORT: invalid lossPerLot={lossPerLot:F5}.");

                return;
            }

            // Volume sized so that hitting the SL loses exactly maxLossUsd
            // (0.5% of balance), then normalized to the broker's volume rules.
            double rawVolume =
                maxLossUsd / lossPerLot;

            double volume =
                NormalizeVolume(rawVolume, symbol);

            Log(
                $"Sizing: " +
                $"tickSize={symbol.TickSize}, " +
                $"tickValue={symbol.TickValue}, " +
                $"structuralSl={structuralSl:F5}, " +
                $"slDistance={slDistance:F5}, " +
                $"maxLossUsd={maxLossUsd:F2}, " +
                $"lossPerLot={lossPerLot:F2}, " +
                $"rawVolume={rawVolume:F5}, " +
                $"volume={volume:F5}.");

            if (volume <= 0)
            {
                Log(
                    $"ABORT: normalized volume is zero.");

                return;
            }

            // Take profit distance is always 2.2x the stop loss distance.
            double tpDistance =
                slDistance * RiskRewardRatio;

            double sl = structuralSl;

            double tp = sell
                ? executionPrice - tpDistance
                : executionPrice + tpDistance;

            Log(
                $"Trade params: " +
                $"side={(sell ? "SELL" : "BUY")}, " +
                $"executionPrice={executionPrice:F5}, " +
                $"SL={sl:F5}, " +
                $"TP={tp:F5}, " +
                $"SLDistance={slDistance:F5}, " +
                $"TPDistance={tpDistance:F5}.");

            var request = new TradeRequestDto
            {
                Symbol = _symbol,
                Volume = volume,
                StopLoss = sl,
                TakeProfit = tp
            };

            TradeResponseDto result;

            if (sell)
            {
                result =
                    await _trading.SellAsync(request);
            }
            else
            {
                result =
                    await _trading.BuyAsync(request);
            }

            Log(
                $"ORDER EXECUTED: " +
                $"side={(sell ? "SELL" : "BUY")}, " +
                $"symbol={_symbol}, " +
                $"ticket={result.Ticket}, " +
                $"volume={volume}, " +
                $"SL={sl:F5}, " +
                $"TP={tp:F5}, " +
                $"executionPrice={executionPrice:F5}.");
        }
        catch (Exception ex)
        {
            Log(
                $"TRADE FAILED: " +
                $"{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            if (acquired)
                _tradeSerial.Release();

            lock (_gate)
            {
                // ========================================================
                // IMPORTANT CHANGE
                // ========================================================
                //
                // DO NOT call ResetAfterTrade() here.
                //
                // R1/R2 are structural data and may become the next Trend
                // even after a successful trade.
                //
                // Therefore we preserve them and promote them.
                //
                PromoteReversalToTrendAfterTrade();

                // Replay any candles that closed while this trade was
                // executing (state was ExecutingTrade), so they are not lost.
                if (_pendingDuringExecution.Count > 0)
                {
                    var pending = _pendingDuringExecution.ToList();
                    _pendingDuringExecution.Clear();

                    Log($"Replaying {pending.Count} candle(s) queued during trade execution.");

                    foreach (var c in pending)
                        ProcessCandle(c);
                }
            }
        }
    }

    private static double NormalizeVolume(
        double volume,
        SymbolInfoResponseDto symbol)
    {
        double min =
            symbol.VolumeMin > 0
                ? symbol.VolumeMin
                : 0;

        double max =
            symbol.VolumeMax > 0
                ? symbol.VolumeMax
                : double.MaxValue;

        double step =
            symbol.VolumeStep > 0
                ? symbol.VolumeStep
                : 0.01;

        double normalized =
            Math.Floor(volume / step) * step;

        return Math.Clamp(
            normalized,
            min,
            max);
    }

    #endregion

    #region Reset

    private void ResetToWaitingForTrend()
    {
        _trend.Clear();
        _reversals.Clear();
        _waitedForEntryPoint.Clear();

        _modeOfTrend = TrendMode.None;
        _modeOfRevers = ReversMode.None;

        _countOfRevers = 0;

        _entry = 0;
        _structuralSl = 0;

        _lastReversalTrend = null;

        _state = StrategyState.WaitingForTrend;

        Log(
            $"State reset -> " +
            $"state={_state}, " +
            $"trendCandles={_trend.Count}, " +
            $"reversalCandles={_reversals.Count}.");
    }

    /// <summary>
    /// Kept for compatibility with the previous implementation.
    ///
    /// Normal trade completion intentionally does NOT use this method,
    /// because reversal data must be preserved for the next trend.
    /// </summary>
    private void ResetAfterTrade()
    {
        ResetToWaitingForTrend();
    }

    #endregion

    #region Public State

    public IReadOnlyList<CandleResponseDto> Trend
    {
        get
        {
            lock (_gate)
                return _trend.ToArray();
        }
    }

    public IReadOnlyList<CandleResponseDto> Reversals
    {
        get
        {
            lock (_gate)
                return _reversals.ToArray();
        }
    }

    public TrendMode CurrentTrend
    {
        get
        {
            lock (_gate)
                return _modeOfTrend;
        }
    }

    public ReversMode CurrentReversal
    {
        get
        {
            lock (_gate)
                return _modeOfRevers;
        }
    }

    public double Entry
    {
        get
        {
            lock (_gate)
                return _entry;
        }
    }

    public int ReversalCount
    {
        get
        {
            lock (_gate)
                return _countOfRevers;
        }
    }

    public bool IsTrendComplete
    {
        get
        {
            lock (_gate)
                return _state == StrategyState.WaitingForEntry;
        }
    }

    public StrategyState CurrentState
    {
        get
        {
            lock (_gate)
                return _state;
        }
    }

    #endregion

    #region Logging

    private void Log(string message)
    {
        Debug.WriteLine(
            $"[ManualStrategy][{_symbol}] {message}");
    }

    #endregion

    #region Enums

    public enum TrendMode
    {
        None,
        UpWard,
        DownWard
    }

    public enum ReversMode
    {
        None,
        Body,
        Shadow
    }

    public enum StrategyState
    {
        WaitingForTrend,

        UpTrend,

        DownTrend,

        WaitingForReversal,

        WaitingForEntry,

        ExecutingTrade
    }

    #endregion
}