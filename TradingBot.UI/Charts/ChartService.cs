using System.Windows;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.WPF;
using Trading.Core.Interfaces;
using Trading.Shared.Events;
using Trading.Shared.Responses;
using TradingBot.UI.Themes;

namespace TradingBot.UI.Charts;

public class ChartService : IChartService
{
    private readonly List<CandleUpdateDto> _candles = [];
    private WpfPlot? _plot;
    private CandlestickPlot? _candlesPlot;
    private ChartTheme? _pendingTheme;
    private Color _candleUp = new(0x2E, 0x9E, 0x5B);
    private Color _candleDown = new(0xC0, 0x39, 0x2B);

    public void Attach(WpfPlot plot)
    {
        _plot = plot;
        _plot.Plot.Title("Trading Bot");

        if (_pendingTheme is not null)
        {
            ApplyTheme(_pendingTheme);
            _pendingTheme = null;
        }
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

            _plot.Plot.FigureBackground.Color = theme.FigureBackground;
            _plot.Plot.DataBackground.Color = theme.FigureBackground;
            _plot.Plot.Axes.Color(theme.AxisText);
            _plot.Plot.Axes.Title.Label.ForeColor = theme.AxisText;
            _plot.Plot.Grid.MajorLineColor = theme.GridLine;
            _plot.Plot.Grid.MinorLineColor = theme.GridMinor;

            if (_candlesPlot is not null)
            {
                _candlesPlot.RisingColor = theme.CandleUp;
                _candlesPlot.FallingColor = theme.CandleDown;
            }

            _plot.Refresh();
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

        _plot.Plot.Clear();

        if (_candles.Count > 0)
        {
            var ohlcs = _candles
                .Select(c => new OHLC(c.Open, c.High, c.Low, c.Close, c.Time, TimeSpan.FromMinutes(1)))
                .ToList();
            _candlesPlot = _plot.Plot.Add.Candlestick(ohlcs);
            _candlesPlot.RisingColor = _candleUp;
            _candlesPlot.FallingColor = _candleDown;

            var canPreserve = preserveZoom
                && !double.IsNaN(viewport.Left) && !double.IsInfinity(viewport.Left)
                && !double.IsNaN(viewport.Right) && !double.IsInfinity(viewport.Right)
                && !double.IsNaN(viewport.Bottom) && !double.IsInfinity(viewport.Bottom)
                && !double.IsNaN(viewport.Top) && !double.IsInfinity(viewport.Top);

            if (canPreserve)
                _plot.Plot.Axes.SetLimits(viewport);
            else
                _plot.Plot.Axes.AutoScale();
        }

        _plot.Refresh();
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
}