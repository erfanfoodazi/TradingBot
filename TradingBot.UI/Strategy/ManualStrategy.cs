using System.Diagnostics;
using Trading.Core.Interfaces;
using Trading.Shared.Events;
using Trading.Shared.Requests;
using Trading.Shared.Responses;

namespace TradingBot.UI.Strategy;

/// <summary>
/// Manual / discretionary strategy.
///
/// Trading logic per the employer's structural rules:
///  - A trend is a sequence of at least <see cref="DefaultMinTrendCandles"/>
///    candles of the SAME direction/color, where every valid trend candle
///    continues the structure by breaking/touching the relevant High (upward)
///    or Low (downward) of the previous trend candle.
///  - Noise candles (a candle that breaks neither the previous High nor Low,
///    or which breaks/touches BOTH of them) are never added to the trend
///    sequence and never count toward the minimum requirements.
///  - A reversal follows the exact same structural rules but in the opposite
///    direction; at least <see cref="DefaultMinReversalCandles"/> valid
///    reversal candles are required before the reversal is confirmed.
///  - Entry levels depend on how the FIRST reversal candle broke the LAST
///    trend candle:
///      * Body break  -> final one-third of the original trend range.
///      * Shadow break -> middle (1/2) of the original trend range.
///    The original trend range is always derived from the trend structure
///    only (never from reversal candles). The SECOND reversal candle only
///    confirms the reversal and never changes the Entry level.
///  - <see cref="Feed(CandleUpdateDto)"/> stays the realtime candle entry point
///    (closed candles drive trend/reversal analysis). Live ticks drive the
///    actual entry via <see cref="OnTick(TickUpdateDto)"/>.
///
/// Architecture preserved:
///  - All shared mutable state is guarded by <see cref="_gate"/> (thread safety).
///  - Duplicate entries are prevented by a deterministic state machine plus a
///    semaphore that serializes Buy/Sell submission.
///  - The symbol always comes from the constructor field <c>_symbol</c>;
///    CandleResponseDto carries no symbol, so it is never read from candles.
///  - <see cref="ITradingService"/> is the trading dependency; the account is
///    read through <see cref="IAccountService"/> for diagnostics only.
///  - Stop Loss comes from the market structure, NOT from a fixed pip value:
///      * UP trend (sell)  -> the structural HIGH of the trend is the SL boundary.
///      * DOWN trend (buy) -> the structural LOW of the trend is the SL boundary.
///    The SL distance is measured from the actual execution price to that
///    structural level, so it adapts to the price structure on every trade.
///  - Risk limit: the expected monetary loss at SL is always approximately
///    $100 (Maximum Loss = $100), so the volume is derived from
///    LossPerLotAtSL = (SL distance / tick size) * tick value
///    and normalized to the broker VolumeMin/VolumeMax/VolumeStep grid.
///  - Take Profit: a fixed 1 : 2.2 risk/reward ratio:
///      TP Distance = SL Distance * 2.2  =>  maximum profit ~ $220.
///  - The P/L math uses the actual MT5 symbol tick specs (trade_tick_value /
///    trade_tick_size), so the realized loss at SL and profit at TP are correct
///    regardless of the symbol or the SL distance.
///    SL is the structural level itself; TP is anchored to the actual current MT5
///    tick price at execution time (Bid for a sell, Ask for a buy) - never to a
///    candle Close.
/// </summary>
public class ManualStrategy
{
    /// <summary>
    /// Minimum number of VALID trend candles required before a reversal setup
    /// can be considered. Noise candles never count toward this value.
    /// </summary>
    public const int DefaultMinTrendCandles = 3;

    /// <summary>
    /// Minimum number of VALID reversal candles required before the reversal
    /// is considered confirmed. Noise candles never count toward this value.
    /// </summary>
    public const int DefaultMinReversalCandles = 1;

    private readonly ITradingService _trading;
    private readonly IAccountService _account;
    private readonly string _symbol;
    private readonly int _minTrendCandles;
    private readonly int _minReversalCandles;
    private readonly object _gate = new();

    /// <summary>Valid trend candles only (same direction/color, no noise).</summary>
    private readonly List<CandleResponseDto> _trend = [];

    /// <summary>Valid reversal candles only (opposite direction, no noise).</summary>
    private readonly List<CandleResponseDto> _reversals = [];

    /// <summary>
    /// The most recently observed sample of the currently forming candle. The live
    /// candle stream pushes the forming bar repeatedly (it opens all-equal and
    /// mutates), so trend detection must only ever consume a bar once it has
    /// <em>closed</em> - i.e. when the next bar's time arrives. This buffer holds
    /// the latest sample of the forming bar until that happens.
    /// </summary>
    private CandleResponseDto? _forming;

    private TrendMode _modeOfTrend = TrendMode.None;
    private ReversMode _modeOfRevers = ReversMode.None;

    /// <summary>Maximum expected monetary loss at the Stop Loss (account currency).</summary>
    private const double MaxLossUsd = 100.0;

    /// <summary>Risk/reward ratio applied to the Stop Loss distance (1 : 2.2).</summary>
    private const double RiskRewardRatio = 2.2;

    private int _countOfRevers;
    private double _entry;

    /// <summary>
    /// Structural Stop Loss boundary derived from the ORIGINAL trend structure only
    /// (never from reversal candles): the trend's structural HIGH for an up trend
    /// (sell) or its structural LOW for a down trend (buy).
    /// </summary>
    private double _structuralSl;

    /// <summary>Throttle window for the periodic WaitingForEntry tick diagnostics.</summary>
    private DateTime _lastTickLogUtc;

    private StrategyState _state = StrategyState.WaitingForTrend;

    public ManualStrategy(ITradingService trading, IAccountService account, string symbol,
        int minTrendCandles = DefaultMinTrendCandles,
        int minReversalCandles = DefaultMinReversalCandles)
    {
        _trading = trading ?? throw new ArgumentNullException(nameof(trading));
        _account = account ?? throw new ArgumentNullException(nameof(account));
        _symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        _minTrendCandles = Math.Max(1, minTrendCandles);
        _minReversalCandles = Math.Max(1, minReversalCandles);
    }

    /// <summary>
    /// Realtime candle entry point. The live candle stream pushes the currently
    /// <em>forming</em> bar every poll: it opens with O=H=L=C and mutates over the
    /// minute, so feeding it straight into trend detection would reset the trend at
    /// every minute boundary and the setup could never complete. Instead the latest
    /// forming sample is buffered and the previous candle - now closed - is only
    /// submitted to trend/reversal detection when the candle time advances. Candle
    /// High/Low are used only to detect trends and reversals - never to trigger the
    /// live entry (ticks do that). Safe to call from any thread.
    /// </summary>
    public void Feed(CandleUpdateDto candle)
    {
        if (candle == null ||
            !string.Equals(candle.Symbol, _symbol, StringComparison.OrdinalIgnoreCase))
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
            TickVolume = candle.TickVolume,
        };

        // Reentrant lock: the decision to promote the buffered bar and ProcessCandle
        // must be atomic. Monitor locks are re-entrant on the same thread, so the
        // inner lock inside ProcessCandle is safe.
        lock (_gate)
        {
            if (_forming is not null && _forming.Time != dto.Time)
            {
                Log($"Feed: closed candle {_forming.Time:HH:mm:ss} (O={_forming.Open}, H={_forming.High}, " +
                    $"L={_forming.Low}, C={_forming.Close}) -> processing.");
                ProcessCandle(_forming);
            }

            _forming = dto;
        }
    }

    /// <summary>
    /// Live entry point: a fresh tick for the tracked symbol. While the strategy
    /// is in <see cref="StrategyState.WaitingForEntry"/> the price is checked
    /// against <see cref="Entry"/> using the tradable side (Bid for sells, Ask for
    /// buys). Safe to call from any thread.
    /// </summary>
    public void OnTick(TickUpdateDto tick)
    {
        if (tick == null ||
            !string.Equals(tick.Symbol, _symbol, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        bool sell = false;
        bool shouldTrade = false;
        bool entryReached = false;
        double executionPrice = 0;

        lock (_gate)
        {
            if (_state != StrategyState.WaitingForEntry)
                return;

            if (_modeOfTrend == TrendMode.UpWard)
                shouldTrade = tick.Bid >= _entry;
            else if (_modeOfTrend == TrendMode.DownWard)
                shouldTrade = tick.Ask <= _entry;
            else
                return;

            entryReached = shouldTrade;

            // Throttled diagnostic so the log shows current Bid/Ask, the state,
            // the Entry level and whether it has been reached - without flooding.
            var now = DateTime.UtcNow;
            if ((now - _lastTickLogUtc).TotalSeconds >= 2.0)
            {
                _lastTickLogUtc = now;
                Log($"WaitingForEntry: state={_state}, trend={_modeOfTrend}, entry={_entry:F5}, " +
                    $"bid={tick.Bid:F5}, ask={tick.Ask:F5}, entryReached={entryReached}");
            }

            if (shouldTrade)
            {
                sell = _modeOfTrend == TrendMode.UpWard;
                executionPrice = sell ? tick.Bid : tick.Ask;
                _state = StrategyState.ExecutingTrade;
                Log($"Entry reached ({(sell ? "SELL" : "BUY")}): entry={_entry:F5}, " +
                    $"executionPrice={executionPrice:F5} (bid={tick.Bid:F5}, ask={tick.Ask:F5}).");
            }
        }

        if (shouldTrade)
            RunTradeAsync(sell, executionPrice);
    }

    /// <summary>
    /// Kept for compatibility: performs trend/reversal detection for one candle.
    /// Lock-guarded, so it is safe to fetch directly. Prefer <see cref="Feed"/>.
    /// </summary>
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

            // A trade is in flight: candles must not disturb the sealed setup.
            if (_state == StrategyState.ExecutingTrade)
                return;

            // Entry resolution is exclusively tick driven; candles keep the setup
            // frozen while we wait for the price.
            if (_state == StrategyState.WaitingForEntry)
                return;

            // No trend yet: seed it from the first coloured candle.
            if (_trend.Count == 0)
            {
                SeedTrend(candle);
                return;
            }

            // Trend is still building (below the confirmation threshold).
            if (_trend.Count < _minTrendCandles)
            {
                ProcessTrendBuilding(candle);
                return;
            }

            // Trend confirmed: watch for a valid reversal.
            ProcessReversalPhase(candle);
        }
    }

    #region Trend detection (structural rules)

    private static bool IsBullish(CandleResponseDto c) => c.Close > c.Open;

    private static bool IsBearish(CandleResponseDto c) => c.Close < c.Open;

    /// <summary>
    /// A candle is "noise" relative to the reference candle when it either
    /// breaks neither the reference High nor Low, or breaks/touches BOTH of
    /// them. Noise candles never enter a trend/reversal sequence and never
    /// count toward the minimum requirements.
    /// </summary>
    private static bool IsNoise(CandleResponseDto candle, CandleResponseDto prev)
    {
        bool breaksHigh = candle.High >= prev.High;
        bool breaksLow = candle.Low <= prev.Low;
        // add new types of noise candles 
        if (prev.Open < prev.Close && candle.High > prev.High && candle.Low > prev.Low && candle.Open > candle.Close )
            return true;

        if (prev.Open > prev.Close && candle.Low < prev.Low && candle.High < prev.High && candle.Open < candle.Close)
            return true;

        double bodySize = Math.Abs(candle.Close - candle.Open);
        double totalRange = candle.High - candle.Low;
        double bodyToRangeRatio = bodySize / totalRange;
        double thresholdPercen = 0.1;

        if(bodyToRangeRatio <= thresholdPercen)
            return true;

        return (!breaksHigh && !breaksLow) || (breaksHigh && breaksLow);
    }

    /// <summary>
    /// Valid upward structural candle: bullish, breaks/touches the relevant
    /// High of the reference candle, and does NOT break/touch the Low (so it
    /// is not an engulfing noise candle).
    /// </summary>
    private static bool IsValidUpward(CandleResponseDto candle, CandleResponseDto prev)
        => IsBullish(candle) && candle.High >= prev.High && candle.Low > prev.Low;

    /// <summary>
    /// Valid downward structural candle: bearish, breaks/touches the relevant
    /// Low of the reference candle, and does NOT break/touch the High (so it
    /// is not an engulfing noise candle).
    /// </summary>
    private static bool IsValidDownward(CandleResponseDto candle, CandleResponseDto prev)
        => IsBearish(candle) && candle.Low <= prev.Low && candle.High < prev.High;

    private void ProcessTrendBuilding(CandleResponseDto candle)
    {
        var last = _trend[^1];

        if (_modeOfTrend == TrendMode.UpWard && IsValidUpward(candle, last))
        {
            _trend.Add(candle);
        }
        else if (_modeOfTrend == TrendMode.DownWard && IsValidDownward(candle, last))
        {
            _trend.Add(candle);
        }
        else if (IsNoise(candle, last))
        {
            // Noise candles are neither counted nor allowed to disturb the trend.
            Log($"Noise candle {candle.Time:HH:mm:ss} skipped (trend={_modeOfTrend}, " +
                $"trendCandles={_trend.Count}/{_minTrendCandles}).");
            return;
        }
        else
        {
            // A non-noise candle that fails to continue the current direction
            // invalidates the unconfirmed trend: restart from this candle.
            Log($"Trend {_modeOfTrend} broken at candle {candle.Time:HH:mm:ss} " +
                $"(H={candle.High}, L={candle.Low} vs last H={last.High}, L={last.Low}); reseeding.");
            SeedTrend(candle);
            return;
        }

        _state = _trend.Count >= _minTrendCandles
            ? StrategyState.WaitingForReversal
            : (_modeOfTrend == TrendMode.UpWard ? StrategyState.UpTrend : StrategyState.DownTrend);

        Log($"Trend {_modeOfTrend}: added candle {candle.Time:HH:mm:ss} -> trendCandles=" +
            $"{_trend.Count}/{_minTrendCandles}, state={_state}.");
    }

    #endregion

    #region Reversal detection (same structural rules, opposite direction)

    private void ProcessReversalPhase(CandleResponseDto candle)
    {
        var lastTrend = _trend[^1];

        // No valid reversal candle recorded yet: the first one must break the
        // LAST trend candle using the opposite-direction structure.
        if (_countOfRevers == 0)
        {
            if (IsFirstReversal(candle, lastTrend))
            {
                _reversals.Add(candle);
                _modeOfRevers = UsesBodyBreak(candle, lastTrend)
                    ? ReversMode.Body
                    : ReversMode.Shadow;

                
               
                _countOfRevers++;
                Log($"First reversal candle {candle.Time:HH:mm:ss} recorded against last trend " +
                    $"(H={lastTrend.High}, L={lastTrend.Low}): type={_modeOfRevers}, " +
                    $"reversalCandles={_countOfRevers}/{_minReversalCandles}, state={_state}.");
                return;
            }

            // The trend is still alive; extend it with a valid continuation.
            if (IsValidTrendContinuation(candle, lastTrend))
            {
                _trend.Add(candle);
                Log($"Trend {_modeOfTrend} extended: +candle {candle.Time:HH:mm:ss} -> " +
                    $"trendCandles={_trend.Count}.");
            }

            return;
        }

        // One valid reversal candle already recorded: need the confirming one.
        var lastReversal = _reversals[^1];
        if (IsNextReversal(candle, lastReversal))
        {
            _reversals.Add(candle);
            _countOfRevers++;
            Log($"Reversal candle {candle.Time:HH:mm:ss} recorded (vs last reversal H={lastReversal.High}, " +
                $"L={lastReversal.Low}) -> reversalCandles={_countOfRevers}/{_minReversalCandles}.");

            if (_countOfRevers >= _minReversalCandles)
            {
                _entry = CalculateEntry();
                _state = StrategyState.WaitingForEntry;
                Log($"REVERSAL CONFIRMED: trend={_modeOfTrend}, reversalType={_modeOfRevers}, " +
                    $"trendCandles={_trend.Count}, reversalCandles={_countOfRevers}, " +
                    $"entry={_entry:F5}, state={_state}. Waiting for price.");
            }

            return;
        }

        // A valid trend continuation invalidates the pending reversal attempt.
        if (IsValidTrendContinuation(candle, lastTrend))
        {
            _trend.Add(candle);
            _reversals.Clear();
            _countOfRevers = 0;
            _modeOfRevers = ReversMode.None;
            Log($"Reversal attempt invalidated by trend continuation candle {candle.Time:HH:mm:ss}; " +
                $"trendCandles={_trend.Count}, reversalCandles=0.");
        }
    }

    /// <summary>
    /// First reversal condition (UpWard trend -> bearish Low break; DownWard
    /// trend -> bullish High break), measured against the last trend candle.
    /// </summary>
    private bool IsFirstReversal(CandleResponseDto candle, CandleResponseDto lastTrend)
    {
        if (_modeOfTrend == TrendMode.UpWard)
        {
            // Uptrend -> first reversal must break/touch
            // the LOW of the last trend candle.
            return IsBearish(candle) &&
                   candle.Low <= lastTrend.Low;
        }

        if (_modeOfTrend == TrendMode.DownWard)
        {
            // Downtrend -> first reversal must break/touch
            // the HIGH of the last trend candle.
            return IsBullish(candle) &&
                   candle.High >= lastTrend.High;
        }

        return false;
    }

    /// <summary>
    /// Subsequent reversal condition, chained against the previous reversal
    /// candle (reversal follows the same structure rules as a trend).
    /// </summary>
    private bool IsNextReversal(CandleResponseDto candle, CandleResponseDto lastReversal)
        => _modeOfTrend == TrendMode.UpWard
            ? IsValidDownward(candle, lastReversal)
            : IsValidUpward(candle, lastReversal);

    private bool IsValidTrendContinuation(CandleResponseDto candle, CandleResponseDto lastTrend)
        => _modeOfTrend switch
        {
            TrendMode.UpWard => IsValidUpward(candle, lastTrend),
            TrendMode.DownWard => IsValidDownward(candle, lastTrend),
            _ => false,
        };

    /// <summary>
    /// Distinguishes a BODY break from a SHADOW/wick break for the FIRST
    /// reversal candle relative to the LAST trend candle:
    ///  - UpWard trend (bearish reversal): the body (Close) pierces below the
    ///    last trend Low.
    ///  - DownWard trend (bullish reversal): the body (Close) pierces above the
    ///    last trend High.
    /// When this returns false, only the shadow/wick broke the level.
    /// </summary>
    private bool UsesBodyBreak(CandleResponseDto candle, CandleResponseDto lastTrend)
        => _modeOfTrend == TrendMode.UpWard
            ? candle.Close < lastTrend.Low
            : candle.Close > lastTrend.High;

    /// <summary>
    /// Entry level from the ORIGINAL trend structure only (never from reversal
    /// candles):
    ///  - Body break  -> final one-third of the trend range.
    ///  - Shadow break -> middle (1/2) of the trend range.
    ///
    /// Also derives the structural Stop Loss boundary from the trend structure:
    ///  - Up trend (sell)  -> the trend's structural HIGH.
    ///  - Down trend (buy) -> the trend's structural LOW.
    /// </summary>
    private double CalculateEntry()
    {
        double entry;
        if (_modeOfTrend == TrendMode.UpWard)
        {
            double low = _trend.First().Low;
            double high = _trend.Last().High;
            entry = _modeOfRevers == ReversMode.Body
                ? low + ((high - low) * 2.0 / 3.0) // final one-third
                : low + ((high - low) / 2.0);      // middle

            _structuralSl = _trend.Max(t => t.High); // structural high -> SL above entry
        }
        else
        {
            double h = _trend.First().High;
            double l = _trend.Last().Low;
            entry = _modeOfRevers == ReversMode.Body
                ? l + ((h - l) / 3.0)              // final one-third
                : l + ((h - l) / 2.0);             // middle

            _structuralSl = _trend.Min(t => t.Low); // structural low -> SL below entry
        }

        var rangeStart = _modeOfTrend == TrendMode.UpWard ? _trend.First().Low : _trend.First().High;
        var rangeEnd = _modeOfTrend == TrendMode.UpWard ? _trend.Last().High : _trend.Last().Low;
        Log($"Entry calc: trend={_modeOfTrend}, trendCandles={_trend.Count}, " +
            $"rangeStart={rangeStart:F5}, rangeEnd={rangeEnd:F5}, range={(rangeEnd - rangeStart):F5}, " +
            $"reversalType={_modeOfRevers} -> entry={entry:F5}, structuralSl={_structuralSl:F5}.");

        return entry;
    }

    #endregion

    #region Trade execution (async, serialized)

    /// <summary>Serializes all order submissions for this strategy instance.</summary>
    private readonly SemaphoreSlim _tradeSerial = new(1, 1);

    private async void RunTradeAsync(bool sell, double executionPrice)
    {
        // The semaphore guarantees that even if two trigger paths raced, only one
        // Buy/Sell request is in flight at a time for this strategy.
        bool acquired = false;
        try
        {
            await _tradeSerial.WaitAsync();
            acquired = true;

            // Diagnostics only: the risk math does not depend on the balance.
            var account = await _account.GetAccountInfoAsync();
            double balance = account.Balance;
            Log($"Account: balance={balance:F2} (risk capped at {MaxLossUsd:F2}, " +
                $"reward capped at {MaxLossUsd * RiskRewardRatio:F2}).");

            // Symbol specs (MT5): point, digits, tick size/value and the
            // broker volume min/max/step constraints.
            var symbol = await _trading.GetSymbolInfoAsync(_symbol);

            // Structural Stop Loss from the original trend structure. This is read
            // under the lock so it stays consistent with the sealed setup.
            double structuralSl;
            lock (_gate)
            {
                structuralSl = _structuralSl;
            }

            // Actual SL price distance from the execution price to the structural
            // level: above the entry for a SELL, below it for a BUY.
            double slDistance = sell
                ? structuralSl - executionPrice   // SL (trend high) above a sell entry
                : executionPrice - structuralSl;  // SL (trend low) below a buy entry
            if (slDistance <= 0)
            {
                Log($"ABORT: structural SL ({structuralSl:F5}) does not protect the " +
                    $"{(sell ? "SELL" : "BUY")} entry ({executionPrice:F5}); slDistance={slDistance:F5}.");
                return;
            }

            // Expected loss on the position for a 1.0 lot move from entry to SL:
            // (SL distance / tick size) ticks * tick value per tick per lot.
            double lossPerLot = (slDistance / symbol.TickSize) * symbol.TickValue;

            // Risk-driven volume so the loss at SL is ~ MaxLossUsd ($100),
            // normalized to the broker volume grid. Smaller SL -> larger volume.
            double rawVolume = MaxLossUsd / lossPerLot;
            double volume = NormalizeVolume(rawVolume, symbol);
            Log($"Sizing: digits={symbol.Digits}, point={symbol.Point}, tickSize={symbol.TickSize}, " +
                $"tickValue={symbol.TickValue}, structuralSl={structuralSl:F5}, slDistance={slDistance:F5}, " +
                $"lossPerLot={lossPerLot:F2}, rawVolume={rawVolume:F5}, volume={volume:F5} " +
                $"(min={symbol.VolumeMin}, max={symbol.VolumeMax}, step={symbol.VolumeStep}).");

            // Take Profit: fixed 1 : 2.2 risk/reward ratio applied to the ACTUAL
            // SL distance (never a fixed pip value).
            double tpDistance = slDistance * RiskRewardRatio;

            // SL is the structural level itself; TP is anchored to the actual
            // market execution price (a BUY fills at Ask, a SELL fills at Bid).
            double sl = structuralSl;
            double tp = sell
                ? executionPrice - tpDistance
                : executionPrice + tpDistance;
            Log($"Trade params: side={(sell ? "SELL" : "BUY")}, executionPrice={executionPrice:F5}, " +
                $"sl={sl:F5} (distance {slDistance:F5}), tp={tp:F5} (distance {tpDistance:F5}, " +
                $"{RiskRewardRatio:F1} : 1 risk/reward).");

            var request = new TradeRequestDto
            {
                Symbol = _symbol,
                Volume = volume,
                StopLoss = sl,
                TakeProfit = tp,
            };

            TradeResponseDto result;
            if (sell)
                result = await _trading.SellAsync(request);
            else
                result = await _trading.BuyAsync(request);

            Log($"ORDER EXECUTED: side={(sell ? "SELL" : "BUY")}, symbol={_symbol}, ticket={result.Ticket}, " +
                $"volume={volume}, sl={sl:F5}, tp={tp:F5}, executionPrice={executionPrice:F5}.");
        }
        catch (Exception ex)
        {
            Log($"TRADE FAILED: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            if (acquired)
                _tradeSerial.Release();

            lock (_gate)
            {
                ResetAfterTrade();
            }
        }
    }

    /// <summary>
    /// Clamps a requested volume to the broker's [VolumeMin, VolumeMax] range and
    /// rounds it down to the nearest <c>VolumeStep</c> multiple.
    /// </summary>
    private static double NormalizeVolume(double volume, SymbolInfoResponseDto symbol)
    {
        double min = symbol.VolumeMin > 0 ? symbol.VolumeMin : 0;
        double max = symbol.VolumeMax > 0 ? symbol.VolumeMax : double.MaxValue;
        double step = symbol.VolumeStep > 0 ? symbol.VolumeStep : 0.01;

        double normalized = Math.Floor(volume / step) * step;
        return Math.Clamp(normalized, min, max);
    }

    #endregion

    #region State reset helpers

    /// <summary>
    /// Clears all state and seeds a fresh trend from the given candle. The trend
    /// direction/color is taken from that candle; a truly neutral candle leaves
    /// the strategy waiting for the first coloured candle.
    /// </summary>
    private void SeedTrend(CandleResponseDto candle)
    {
        _trend.Clear();
        _reversals.Clear();

        _modeOfRevers = ReversMode.None;
        _countOfRevers = 0;

        _entry = 0;
        _structuralSl = 0;

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

        _state = _trend.Count == 0
            ? StrategyState.WaitingForTrend
            : _trend.Count >= _minTrendCandles
                ? StrategyState.WaitingForReversal
                : (_modeOfTrend == TrendMode.UpWard
                    ? StrategyState.UpTrend
                    : StrategyState.DownTrend);

        Log($"SeedTrend: candle {candle.Time:HH:mm:ss} (O={candle.Open}, H={candle.High}, " +
            $"L={candle.Low}, C={candle.Close}) -> trend={_modeOfTrend}, trendCandles={_trend.Count}, " +
            $"state={_state}.");
    }

    private void ResetAfterTrade()
    {
        _trend.Clear();
        _reversals.Clear();

        _modeOfTrend = TrendMode.None;
        _modeOfRevers = ReversMode.None;

        _entry = 0;
        _structuralSl = 0;
        _countOfRevers = 0;

        _state = StrategyState.WaitingForTrend;

        Log($"State reset after trade -> state={_state}, trendCandles={_trend.Count}, " +
            $"reversalCandles={_reversals.Count}.");
    }

    #endregion

    #region Public read-only state (MVVM-friendly, lock-guarded)

    public IReadOnlyList<CandleResponseDto> Trend
    {
        get
        {
            lock (_gate) return _trend.ToArray();
        }
    }

    public TrendMode CurrentTrend
    {
        get
        {
            lock (_gate) return _modeOfTrend;
        }
    }

    public ReversMode CurrentReversal
    {
        get
        {
            lock (_gate) return _modeOfRevers;
        }
    }

    public double Entry
    {
        get
        {
            lock (_gate) return _entry;
        }
    }

    public int ReversalCount
    {
        get
        {
            lock (_gate) return _countOfRevers;
        }
    }

    public bool IsTrendComplete
    {
        get
        {
            lock (_gate) return _state == StrategyState.WaitingForEntry;
        }
    }

    /// <summary>Current state machine state (for UI/debug).</summary>
    public StrategyState CurrentState
    {
        get
        {
            lock (_gate) return _state;
        }
    }

    #endregion

    /// <summary>
    /// Writes a structured debug line for this strategy instance. Every call is
    /// safe from any thread (<see cref="Debug.WriteLine"/> is thread-safe).
    /// </summary>
    private void Log(string message)
        => Debug.WriteLine($"[ManualStrategy][{_symbol}] {message}");

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

    /// <summary>Deterministic state machine.</summary>
    public enum StrategyState
    {
        /// <summary>No usable trend yet; candles are being accumulated.</summary>
        WaitingForTrend,

        /// <summary>An upward trend is being formed (below the candle threshold).</summary>
        UpTrend,

        /// <summary>A downward trend is being formed (below the candle threshold).</summary>
        DownTrend,

        /// <summary>The setup is complete and the strategy waits for the reversal order.</summary>
        WaitingForReversal,

        /// <summary>A reversal setup exists and the price has not reached the entry yet.</summary>
        WaitingForEntry,

        /// <summary>A trade is in flight; no further entries are accepted.</summary>
        ExecutingTrade
    }
}
