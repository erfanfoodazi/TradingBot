using System.Windows;
using System.Windows.Input;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WPF;
using Trading.Core.Indicators;
using Trading.Core.Interfaces;
using Trading.Shared.Events;
using Trading.Shared.Responses;
using TradingBot.UI.Themes;

namespace TradingBot.UI.Charts;

public class ChartService : IChartService
{
    private const int SmaPeriod = 20;
    private const int EmaPeriod = 20;
    private const int BollingerPeriod = 20;
    private const int AtrPeriod = 14;
    private const int RsiPeriod = 14;

    // Shared Y-axis width (px) so the price plot area lines up exactly with
    // the oscillator strip below it, regardless of label text width.
    private const float SharedAxisWidth = 70;

    private readonly List<CandleUpdateDto> _candles = [];
    private readonly HashSet<IndicatorType> _indicators = [];
    private List<BackTestMarker> _markers = [];
    private WpfPlot? _plot;
    private WpfPlot? _oscPlot;
    private CandlestickPlot? _candlesPlot;
    private ChartTheme? _pendingTheme;
    private Color _candleUp = new(0x2E, 0x9E, 0x5B);
    private Color _candleDown = new(0xC0, 0x39, 0x2B);

    // Indicator palette.
    private static readonly Color SmaColor = new(0x1F, 0x77, 0xB4);
    private static readonly Color EmaColor = new(0xFF, 0x7F, 0x0E);
    private static readonly Color BollingerMiddleColor = new(0x7F, 0x7F, 0x7F);
    private static readonly Color BollingerBandColor = new(0x2C, 0xA0, 0x2C);
    private static readonly Color VwapColor = new(0x94, 0x64, 0xC6);
    private static readonly Color AtrColor = new(0x8B, 0x45, 0x13);
    private static readonly Color FibonacciColor = new(0x8E, 0x8E, 0x8E);
    private static readonly Color TenkanColor = new(0x1F, 0x77, 0xB4);
    private static readonly Color KijunColor = new(0xFF, 0x7F, 0x0E);
    private static readonly Color SenkouAColor = new(0x2C, 0xA0, 0x2C);
    private static readonly Color SenkouBColor = new(0xD6, 0x27, 0x28);
    private static readonly Color ChikouColor = new(0x7F, 0x7F, 0x7F);
    private static readonly Color CloudColor = new(0x2C, 0xA0, 0x2C, 60);
    private static readonly Color RsiColor = new(0x1F, 0x77, 0xB4);
    private static readonly Color StochKColor = new(0x1F, 0x77, 0xB4);
    private static readonly Color StochDColor = new(0xFF, 0x7F, 0x0E);
    private static readonly Color MacdColor = new(0x1F, 0x77, 0xB4);
    private static readonly Color MacdSignalColor = new(0xFF, 0x7F, 0x0E);
    private static readonly Color MacdHistogramColor = new(0x64, 0x64, 0x64, 160);

    public void Attach(WpfPlot plot)
    {
        _plot = plot;
        _plot.Plot.Title("Trading Bot");

        // Show real date/time labels on the bottom axis, matching the
        // oscillator strip so both charts read on the same timeline.
        _plot.Plot.Axes.DateTimeTicksBottom();

        // Pin the Y-axis width so the candle plot area is identical to the
        // oscillator strip below it.
        _plot.Plot.Axes.Left.MinimumSize = SharedAxisWidth;
        _plot.Plot.Axes.Left.MaximumSize = SharedAxisWidth;

        // Keep the oscillator strip in sync when the user pans/zooms the chart.
        _plot.AddHandler(
            Mouse.MouseWheelEvent,
            new MouseWheelEventHandler((_, _) => SyncOscillatorAxes()),
            handledEventsToo: true);
        _plot.MouseUp += (_, _) => SyncOscillatorAxes();

        if (_pendingTheme is not null)
        {
            ApplyTheme(_pendingTheme);
            _pendingTheme = null;
        }
    }

    public void AttachOscillator(WpfPlot plot)
    {
        _oscPlot = plot;
        _oscPlot.Plot.Title("Indicator");
        _oscPlot.UserInputProcessor.Disable();
        _oscPlot.Plot.Axes.DateTimeTicksBottom();

        // Same Y-axis width as the main chart so both plot areas align.
        _oscPlot.Plot.Axes.Left.MinimumSize = SharedAxisWidth;
        _oscPlot.Plot.Axes.Left.MaximumSize = SharedAxisWidth;

        if (_pendingTheme is not null)
            ApplyThemeToPlot(plot, _pendingTheme);
    }

    public void ApplyTheme(ChartTheme theme)
    {
        if (_plot is null)
        {
            _pendingTheme = theme;
            return;
        }

        OnUiThread(() =>
        {
            _candleUp = theme.CandleUp;
            _candleDown = theme.CandleDown;

            if (_plot is null)
                return;

            ApplyThemeToPlot(_plot, theme);
            if (_oscPlot is not null)
                ApplyThemeToPlot(_oscPlot, theme);

            if (_candlesPlot is not null)
            {
                _candlesPlot.RisingColor = theme.CandleUp;
                _candlesPlot.FallingColor = theme.CandleDown;
            }

            _plot.Refresh();
            _oscPlot?.Refresh();
        });
    }

    private static void ApplyThemeToPlot(WpfPlot plot, ChartTheme theme)
    {
        plot.Plot.FigureBackground.Color = theme.FigureBackground;
        plot.Plot.DataBackground.Color = theme.FigureBackground;
        plot.Plot.Axes.Color(theme.AxisText);
        plot.Plot.Axes.Title.Label.ForeColor = theme.AxisText;
        plot.Plot.Grid.MajorLineColor = theme.GridLine;
        plot.Plot.Grid.MinorLineColor = theme.GridMinor;
    }

    public void SetIndicators(IEnumerable<IndicatorType> indicators)
    {
        OnUiThread(() =>
        {
            _indicators.Clear();
            foreach (var indicator in indicators)
                _indicators.Add(indicator);

            Render(preserveZoom: true);
        });
    }

    public void LoadCandles(IEnumerable<CandleResponseDto> candles)
    {
        if (_plot is null)
            return;

        var list = candles.ToList();
        if (list.Count == 0)
            return;

        OnUiThread(() =>
        {
            _candles.Clear();
            _candles.AddRange(list.Select(c => new CandleUpdateDto
            {
                Time = c.Time,
                Open = c.Open,
                High = c.High,
                Low = c.Low,
                Close = c.Close,
                TickVolume = c.TickVolume,
            }));
            Render(preserveZoom: false);
        });
    }

    public void AddCandle(CandleUpdateDto candle)
    {
        if (_plot is null)
            return;

        OnUiThread(() =>
        {
            // Replace the last (forming) bar if the tick belongs to it.
            if (_candles.Count > 0 && _candles[^1].Time == candle.Time)
                _candles[^1] = candle;
            else
                _candles.Add(candle);

            TrimToWindow();

            Render(preserveZoom: true);
        });
    }

    public void Clear()
    {
        OnUiThread(() =>
        {
            _candles.Clear();
            Render(preserveZoom: false);
        });
    }

    /// <summary>
    /// Overlays the entry / SL / TP markers of a finished backtest on the chart.
    /// </summary>
    public void SetBackTestMarkers(IEnumerable<BackTestMarker> markers)
    {
        OnUiThread(() =>
        {
            _markers = markers.ToList();
            Render(preserveZoom: false);
        });
    }

    public void ClearBackTestMarkers()
    {
        OnUiThread(() =>
        {
            _markers = [];
            Render(preserveZoom: false);
        });
    }

    private void TrimToWindow()
    {
        const int maxVisible = 1000;
        if (_candles.Count > maxVisible)
            _candles.RemoveRange(0, _candles.Count - maxVisible);
    }

    private void Render(bool preserveZoom)
    {
        if (_plot is null)
            return;

        // Keep the user's current viewport so live updates don't reset
        // the zoom/pan state (captured before the plot is cleared).
        var viewport = _plot.Plot.Axes.GetLimits();
        bool canPreserve = preserveZoom && IsValid(viewport);

        _plot.Plot.Clear();
        _candlesPlot = null;

        if (_candles.Count > 0)
        {
            // Derive the candle width from the actual spacing between bars so
            // larger timeframes (H1, H4, D1, ...) stay compact instead of
            // rendering wildly separated candles.
            var candleSpan = EstimateCandleSpan(_candles);

            var ohlcs = _candles
                .Select(c => new OHLC(c.Open, c.High, c.Low, c.Close, c.Time, candleSpan))
                .ToList();
            _candlesPlot = _plot.Plot.Add.Candlestick(ohlcs);
            _candlesPlot.RisingColor = _candleUp;
            _candlesPlot.FallingColor = _candleDown;

            var indicatorCandles = ToIndicatorCandles(_candles);
            if (_indicators.Count > 0)
                RenderPriceIndicators(_plot.Plot, indicatorCandles);
        }

        RenderBackTestMarkers(_plot.Plot);

        if (canPreserve)
            _plot.Plot.Axes.SetLimits(viewport);
        else
            _plot.Plot.Axes.AutoScale();

        _plot.Refresh();

        RenderOscillators(ToIndicatorCandles(_candles), viewport);
    }

    /// <summary>
    /// Draws backtest entry / SL / TP markers on the price plot.
    /// </summary>
    private void RenderBackTestMarkers(Plot plot)
    {
        if (_markers.Count == 0)
            return;

        var entries = new List<(double x, double y)>();
        var stops = new List<(double x, double y)>();
        var takes = new List<(double x, double y)>();

        foreach (var marker in _markers)
        {
            double x = NumericConversion.ToNumber(marker.Time);
            switch (marker.Kind)
            {
                case BackTestMarkerKind.Entry:
                    entries.Add((x, marker.Price));
                    break;
                case BackTestMarkerKind.StopLoss:
                    stops.Add((x, marker.Price));
                    break;
                case BackTestMarkerKind.TakeProfit:
                    takes.Add((x, marker.Price));
                    break;
            }
        }

        AddMarkers(plot, entries, new Color(0x1F, 0x77, 0xB4), MarkerShape.FilledCircle, 7);
        AddMarkers(plot, stops, new Color(0xC0, 0x39, 0x2B), MarkerShape.FilledTriangleDown, 7);
        AddMarkers(plot, takes, new Color(0x2E, 0x9E, 0x5B), MarkerShape.FilledTriangleUp, 7);
    }

    private static void AddMarkers(
        Plot plot,
        List<(double x, double y)> points,
        Color color,
        MarkerShape shape,
        float size)
    {
        if (points.Count == 0)
            return;

        var scatter = plot.Add.Scatter(
            points.Select(p => p.x).ToArray(),
            points.Select(p => p.y).ToArray(),
            color);
        scatter.LineWidth = 0;
        scatter.MarkerShape = shape;
        scatter.MarkerSize = size;
    }

    /// <summary>
    /// Renders indicators that share the price scale directly on top of the
    /// candlesticks. All series are index-aligned with the candles so they stay
    /// perfectly in sync while the last bar forms in real time.
    /// </summary>
    private void RenderPriceIndicators(Plot plot, List<CandleResponseDto> candles)
    {
        var xs = candles.Select(c => NumericConversion.ToNumber(c.Time)).ToArray();

        if (_indicators.Contains(IndicatorType.Sma))
        {
            var values = TechnicalIndicators.Sma(candles, SmaPeriod);
            AddLine(plot, ToPoints(values, xs), SmaColor, 1.5f);
        }

        if (_indicators.Contains(IndicatorType.Ema))
        {
            var values = TechnicalIndicators.Ema(candles, EmaPeriod);
            AddLine(plot, ToPoints(values, xs), EmaColor, 1.5f);
        }

        if (_indicators.Contains(IndicatorType.BollingerBands))
        {
            var bands = TechnicalIndicators.BollingerBands(candles, BollingerPeriod);
            AddLine(plot, ToPoints(bands.Upper, xs), BollingerBandColor, 1f);
            AddLine(plot, ToPoints(bands.Middle, xs), BollingerMiddleColor, 1.2f);
            AddLine(plot, ToPoints(bands.Lower, xs), BollingerBandColor, 1f);
        }

        if (_indicators.Contains(IndicatorType.Vwap))
        {
            var values = TechnicalIndicators.Vwap(candles);
            AddLine(plot, ToPoints(values, xs), VwapColor, 1.5f);
        }

        if (_indicators.Contains(IndicatorType.Atr))
        {
            var values = TechnicalIndicators.Atr(candles, AtrPeriod);
            AddLine(plot, ToPoints(values, xs), AtrColor, 1.5f);
        }

        if (_indicators.Contains(IndicatorType.Fibonacci))
        {
            var fib = TechnicalIndicators.FibonacciRetracement(candles, Math.Min(candles.Count, 500));
            foreach (var level in fib.Levels)
            {
                var line = plot.Add.HorizontalLine(level.Value, 1f, FibonacciColor, LinePattern.Dashed);
                line.Text = level.Key.ToString("0.000");
            }
        }

        if (_indicators.Contains(IndicatorType.Ichimoku))
        {
            var ichimoku = TechnicalIndicators.Ichimoku(candles);

            AddLine(plot, ToPoints(ichimoku.TenkanSen, xs), TenkanColor, 1.2f);
            AddLine(plot, ToPoints(ichimoku.KijunSen, xs), KijunColor, 1.2f);
            AddLine(plot, ToPoints(ichimoku.SenkouSpanA, xs), SenkouAColor, 1f);
            AddLine(plot, ToPoints(ichimoku.SenkouSpanB, xs), SenkouBColor, 1f);
            AddLine(plot, ToPoints(ichimoku.ChikouSpan, xs), ChikouColor, 1f);

            var (cloudXs, spanA, spanB) = ToFillPoints(ichimoku.SenkouSpanA, ichimoku.SenkouSpanB, xs);
            if (cloudXs.Length > 0)
                plot.Add.FillY(cloudXs, spanA, spanB).FillColor = CloudColor;
        }
    }

    /// <summary>
    /// Renders RSI / Stochastic / MACD in the dedicated oscillator strip below
    /// the main chart. The strip shares the main chart's horizontal viewport so
    /// zooming/panning the price chart keeps both in lock-step.
    /// </summary>
    private void RenderOscillators(List<CandleResponseDto> candles, AxisLimits viewport)
    {
        if (_oscPlot is null)
            return;

        var plot = _oscPlot.Plot;
        plot.Clear();

        var any = false;
        var xs = candles.Select(c => NumericConversion.ToNumber(c.Time)).ToArray();

        if (candles.Count > 0)
        {
            if (_indicators.Contains(IndicatorType.Rsi))
            {
                var values = TechnicalIndicators.Rsi(candles, RsiPeriod);
                AddLine(plot, ToPoints(values, xs), RsiColor, 1.5f);
                any = true;
            }

            if (_indicators.Contains(IndicatorType.Stochastic))
            {
                var stoch = TechnicalIndicators.Stochastic(candles);
                AddLine(plot, ToPoints(stoch.K, xs), StochKColor, 1.5f);
                AddLine(plot, ToPoints(stoch.D, xs), StochDColor, 1.5f);
                any = true;
            }

            if (_indicators.Contains(IndicatorType.Macd))
            {
                var macd = TechnicalIndicators.Macd(candles);
                AddLine(plot, ToPoints(macd.MacdLine, xs), MacdColor, 1.5f);
                AddLine(plot, ToPoints(macd.SignalLine, xs), MacdSignalColor, 1.5f);

                var histXs = new List<double>();
                var histYs = new List<double>();
                for (int i = 0; i < macd.Histogram.Count; i++)
                {
                    if (macd.Histogram[i].HasValue)
                    {
                        histXs.Add(xs[i]);
                        histYs.Add(macd.Histogram[i]!.Value);
                    }
                }

                if (histXs.Count > 0)
                {
                    var bars = plot.Add.Bars(histXs.ToArray(), histYs.ToArray());
                    bars.Color = MacdHistogramColor;
                }
                any = true;
            }
        }

        if (any)
        {
            plot.Axes.AutoScale();

            // RSI and Stochastic are bounded (0..100) - pin the Y axis so the
            // oscillator reads naturally instead of stretching to the data.
            if (_indicators.Contains(IndicatorType.Rsi) || _indicators.Contains(IndicatorType.Stochastic))
            {
                var limits = plot.Axes.GetLimits();
                plot.Axes.SetLimits(limits.Left, limits.Right, 0, 100);
            }

            SyncOscillatorX(viewport);
            _oscPlot.Refresh();
        }
        else
        {
            _oscPlot.Refresh();
        }
    }

    private void SyncOscillatorAxes()
    {
        if (_plot is null || _oscPlot is null)
            return;

        SyncOscillatorX(_plot.Plot.Axes.GetLimits());
        _oscPlot.Refresh();
    }

    private void SyncOscillatorX(AxisLimits viewport)
    {
        if (_oscPlot is null || !IsValid(viewport))
            return;

        var y = _oscPlot.Plot.Axes.GetLimits();
        _oscPlot.Plot.Axes.SetLimits(viewport.Left, viewport.Right, y.Bottom, y.Top);
    }

    private static bool IsValid(AxisLimits limits)
        => !double.IsNaN(limits.Left) && !double.IsInfinity(limits.Left)
        && !double.IsNaN(limits.Right) && !double.IsInfinity(limits.Right)
        && !double.IsNaN(limits.Bottom) && !double.IsInfinity(limits.Bottom)
        && !double.IsNaN(limits.Top) && !double.IsInfinity(limits.Top);

    private static List<CandleResponseDto> ToIndicatorCandles(IEnumerable<CandleUpdateDto> candles)
        => candles.Select(c => new CandleResponseDto
        {
            Time = c.Time,
            Open = c.Open,
            High = c.High,
            Low = c.Low,
            Close = c.Close,
            TickVolume = c.TickVolume,
        }).ToList();

    private static void AddLine(
        Plot plot,
        (double[] xs, double[] ys) points,
        Color color,
        float width,
        LinePattern pattern = default)
    {
        if (points.ys.Length == 0)
            return;

        var scatter = plot.Add.Scatter(points.xs, points.ys, color);
        scatter.LineWidth = width;
        scatter.LinePattern = pattern;
        scatter.MarkerSize = 0;
    }

    /// <summary>
    /// Drops null samples so ScottPlot only connects points where the indicator
    /// actually has a value (indicators yield null until enough history exists).
    /// </summary>
    private static (double[] xs, double[] ys) ToPoints(List<double?> series, double[] xs)
    {
        var px = new List<double>();
        var py = new List<double>();

        for (int i = 0; i < series.Count; i++)
        {
            if (series[i].HasValue)
            {
                px.Add(xs[i]);
                py.Add(series[i]!.Value);
            }
        }

        return (px.ToArray(), py.ToArray());
    }

    /// <summary>
    /// Builds the two series used to fill the Ichimoku cloud, keeping only the
    /// indices where both spans have values.
    /// </summary>
    private static (double[] xs, double[] ys1, double[] ys2) ToFillPoints(
        List<double?> seriesA,
        List<double?> seriesB,
        double[] xs)
    {
        var px = new List<double>();
        var py1 = new List<double>();
        var py2 = new List<double>();

        for (int i = 0; i < xs.Length; i++)
        {
            if (seriesA[i].HasValue && seriesB[i].HasValue)
            {
                px.Add(xs[i]);
                py1.Add(seriesA[i]!.Value);
                py2.Add(seriesB[i]!.Value);
            }
        }

        return (px.ToArray(), py1.ToArray(), py2.ToArray());
    }

    private static void OnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }

    /// <summary>
    /// Estimates a compact candle width from the smallest positive gap between
    /// consecutive candles and leaves a small margin so the body and wick of
    /// each candle stay clearly distinguishable.
    /// </summary>
    private static TimeSpan EstimateCandleSpan(List<CandleUpdateDto> candles)
    {
        TimeSpan? smallest = null;

        for (var i = 1; i < candles.Count; i++)
        {
            var delta = candles[i].Time - candles[i - 1].Time;
            if (delta > TimeSpan.Zero && (smallest is null || delta < smallest.Value))
                smallest = delta;
        }

        // Fall back to one minute when the spacing cannot be derived (e.g. a
        // single candle or gaps), then reserve 20% as inter-candle gutters.
        return TimeSpan.FromTicks((smallest ?? TimeSpan.FromMinutes(1)).Ticks * 4 / 5);
    }
}