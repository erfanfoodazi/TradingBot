using System.Diagnostics;
using Trading.Core.Interfaces;
using Trading.Shared.Events;
using Trading.Shared.Requests;
using Trading.Shared.Responses;

namespace TradingBot.UI.Strategy;

/// <summary>
/// Manual / discretionary strategy.
///
/// Original trading logic preserved:
///  - Candles build and detect the trend and the reversing candle.
///  - Entry levels are computed with the existing Body/Shadow midpoint rules.
///  - <see cref="Feed(CandleUpdateDto)"/> stays the realtime candle entry point.
///
/// Improvements:
///  - All shared mutable state is guarded by <see cref="_gate"/> (thread safety).
///  - A deterministic state machine (booleans/enums were replaced) prevents
///    duplicate trades and makes every transition explicit.
///  - Real-time entry is driven by live ticks via <see cref="OnTick(TickUpdateDto)"/>
///    instead of relying only on candle High/Low for entry execution.
///  - Trade submission is serialized; multiple Buy/Sell calls cannot overlap.
///  - The symbol always comes from the constructor field <c>_symbol</c>;
///    CandleResponseDto carries no symbol, so it is never read from candles.
/// </summary>
public class ManualStrategy
{
    /// <summary>
    /// Minimum number of trend candles required before a reversing candle can
    /// complete the setup. Configurable through the constructor; defaults to the
    /// original value of 3 (the rule is not assumed, it is a parameter).
    /// </summary>
    public const int DefaultMinTrendCandles = 3;

    private readonly ITradingService _trading;
    private readonly string _symbol;
    private readonly int _minTrendCandles;
    private readonly object _gate = new();

    private readonly List<CandleResponseDto> _trend = [];

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

    private int _countOfRevers;
    private double _entry;

    private StrategyState _state = StrategyState.WaitingForTrend;

    public ManualStrategy(ITradingService trading, string symbol,
        int minTrendCandles = DefaultMinTrendCandles)
    {
        _trading = trading ?? throw new ArgumentNullException(nameof(trading));
        _symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        _minTrendCandles = Math.Max(1, minTrendCandles);
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
                ProcessCandle(_forming);

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

            if (shouldTrade)
            {
                sell = _modeOfTrend == TrendMode.UpWard;
                _state = StrategyState.ExecutingTrade;
            }
        }

        if (shouldTrade)
            RunTradeAsync(sell);
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

            if (_trend.Count == 0)
            {
                _trend.Add(candle);
                _state = StrategyState.WaitingForTrend;
                return;
            }

            // Only after enough trend candles have accumulated may a candle
            // complete the setup by reversing. (Mirrors the original >= limit.)
            if (_trend.Count >= _minTrendCandles)
            {
                CheckForReversTrend(candle);

                if (_state == StrategyState.WaitingForEntry)
                    return;
            }

            if (CheckForUpwardTrend(candle))
            {
                _modeOfTrend = TrendMode.UpWard;
                _trend.Add(candle);
                _state = _trend.Count >= _minTrendCandles
                    ? StrategyState.WaitingForReversal
                    : StrategyState.UpTrend;
                return;
            }

            if (CheckForDownwardTrend(candle))
            {
                _modeOfTrend = TrendMode.DownWard;
                _trend.Add(candle);
                _state = _trend.Count >= _minTrendCandles
                    ? StrategyState.WaitingForReversal
                    : StrategyState.DownTrend;
                return;
            }

            ResetTrend(candle);
        }
    }

    #region Trend detection (unchanged candle maths)

    private bool CheckForUpwardTrend(CandleResponseDto candle)
    {
        var previous = _trend.Last();

        bool bullish = candle.Close > candle.Open;
        if (!bullish)
            return false;

        return previous.Low < candle.Low &&
               previous.High < candle.High;
    }

    private bool CheckForDownwardTrend(CandleResponseDto candle)
    {
        var previous = _trend.Last();

        bool bearish = candle.Close < candle.Open;
        if (!bearish)
            return false;

        return previous.Low > candle.Low &&
               previous.High > candle.High;
    }

    private void CheckForReversTrend(CandleResponseDto candle)
    {
        var previous = _trend.Last();

        bool bullish = candle.Close > candle.Open;
        bool bearish = candle.Close < candle.Open;

        if (_modeOfTrend == TrendMode.UpWard)
        {
            if (bearish && previous.Low > candle.Low)
            {
                if (previous.Low > candle.Close)
                {
                    _modeOfRevers = ReversMode.Body;
                    _entry = CalculateUpTrendBodyEntry();
                }
                else
                {
                    _modeOfRevers = ReversMode.Shadow;
                    _entry = CalculateUpTrendShadowEntry();
                }

                _trend.Add(candle);
                _countOfRevers++;
                _state = StrategyState.WaitingForEntry;
                return;
            }
        }

        if (_modeOfTrend == TrendMode.DownWard)
        {
            if (bullish && previous.High < candle.High)
            {
                if (previous.High < candle.Close)
                {
                    _modeOfRevers = ReversMode.Body;
                    _entry = CalculateDownTrendBodyEntry();
                }
                else
                {
                    _modeOfRevers = ReversMode.Shadow;
                    _entry = CalculateDownTrendShadowEntry();
                }

                _trend.Add(candle);
                _countOfRevers++;
                _state = StrategyState.WaitingForEntry;
                return;
            }
        }
    }

    // Each routine returns a single real price level (a scalar), not a range.
    private double CalculateUpTrendBodyEntry()
    {
        double low = _trend.First().Low;
        double high = _trend.Last().High;
        return low + ((high - low) / 2.0); // midpoint of the range
    }

    private double CalculateUpTrendShadowEntry()
    {
        double low = _trend.First().Low;
        double high = _trend.Last().High;
        return low + (((high - low) / 3.0) * 2.0); // 2/3 into the range
    }

    private double CalculateDownTrendBodyEntry()
    {
        double high = _trend.First().High;
        double low = _trend.Last().Low;
        return low + ((high - low) / 2.0); // midpoint of the range
    }

    private double CalculateDownTrendShadowEntry()
    {
        double high = _trend.First().High;
        double low = _trend.Last().Low;
        return low + ((high - low) / 3.0); // 1/3 up from the range low
    }

    #endregion

    #region Trade execution (async, serialized)

    /// <summary>Serializes all order submissions for this strategy instance.</summary>
    private readonly SemaphoreSlim _tradeSerial = new(1, 1);

    private async void RunTradeAsync(bool sell)
    {
        // The semaphore guarantees that even if two trigger paths raced, only one
        // Buy/Sell request is in flight at a time for this strategy.
        bool acquired = false;
        try
        {
            await _tradeSerial.WaitAsync();
            acquired = true;

            const double volume = 0.01;
            var request = new TradeRequestDto
            {
                Symbol = _symbol,
                Volume = volume,
            };

            if (sell)
                await _trading.SellAsync(request);
            else
                await _trading.BuyAsync(request);

            Debug.WriteLine($"[ManualStrategy] order for {_symbol} executed.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ManualStrategy] trade failed: {ex.Message}");
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

    #endregion

    #region State reset helpers

    private void ResetTrend(CandleResponseDto candle)
    {
        _trend.Clear();
        _trend.Add(candle);

        _modeOfTrend = TrendMode.None;
        _modeOfRevers = ReversMode.None;

        _entry = 0;
        _countOfRevers = 0;

        _state = StrategyState.WaitingForTrend;
    }

    private void ResetAfterTrade()
    {
        _trend.Clear();

        _modeOfTrend = TrendMode.None;
        _modeOfRevers = ReversMode.None;

        _entry = 0;
        _countOfRevers = 0;

        _state = StrategyState.WaitingForTrend;
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