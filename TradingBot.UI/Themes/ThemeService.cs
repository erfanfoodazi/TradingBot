using System.Windows;
using System.Windows.Media;
using TradingBot.UI.Charts;

namespace TradingBot.UI.Themes;

/// <summary>
/// Applies a theme to the WPF surfaces (via Application resources) and to the
/// ScottPlot chart.
/// </summary>
public sealed class ThemeService
{
    private readonly ChartService _chartService;

    public ThemeService(ChartService chartService)
    {
        _chartService = chartService;
    }

    public void Apply(AppTheme theme)
    {
        var palette = Palette.For(theme);

        var app = Application.Current;
        if (app is not null)
        {
            Set(app, "ThemeWindowBackground", palette.WindowBackground);
            Set(app, "ThemePanelBackground", palette.PanelBackground);
            Set(app, "ThemeBorderBrush", palette.Border);
            Set(app, "ThemeTextBrush", palette.Text);
            Set(app, "ThemeAccentBrush", palette.Accent);
            Set(app, "ThemeRowAlternating", palette.AlternatingRow);
            Set(app, "ThemeUpBrush", palette.Up);
            Set(app, "ThemeDownBrush", palette.Down);
            Set(app, "ThemeHoverBrush", palette.Hover);
            Set(app, "ThemePressedBrush", palette.Pressed);
            Set(app, "ThemeDisabledBackgroundBrush", palette.DisabledBackground);
            Set(app, "ThemeDisabledForegroundBrush", palette.DisabledForeground);
        }

        _chartService.ApplyTheme(palette.Chart);
    }

    private static void Set(Application app, string key, Color color)
        => app.Resources[key] = new SolidColorBrush(color);
}
