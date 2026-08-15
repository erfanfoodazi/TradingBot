using ScottPlot;

namespace TradingBot.UI.Themes;

/// <summary>Colors used to style the ScottPlot chart.</summary>
public sealed class ChartTheme
{
    public Color FigureBackground { get; init; }
    public Color AxisText { get; init; }
    public Color GridLine { get; init; }
    public Color GridMinor { get; init; }
    public Color CandleUp { get; init; }
    public Color CandleDown { get; init; }
}

/// <summary>Color set applied to the WPF surfaces.</summary>
public sealed class AppPalette
{
    public System.Windows.Media.Color WindowBackground { get; init; }
    public System.Windows.Media.Color PanelBackground { get; init; }
    public System.Windows.Media.Color Border { get; init; }
    public System.Windows.Media.Color Text { get; init; }
    public System.Windows.Media.Color Accent { get; init; }
    public System.Windows.Media.Color AlternatingRow { get; init; }
    public System.Windows.Media.Color Up { get; init; }
    public System.Windows.Media.Color Down { get; init; }
    public System.Windows.Media.Color Hover { get; init; }
    public System.Windows.Media.Color Pressed { get; init; }
    public System.Windows.Media.Color DisabledBackground { get; init; }
    public System.Windows.Media.Color DisabledForeground { get; init; }
    public ChartTheme Chart { get; init; } = null!;
}

public enum AppTheme
{
    Light,
    Dark,
    Blue,
    Midnight,
}

public static class Palette
{
    public static AppPalette For(AppTheme theme) => theme switch
    {
        AppTheme.Light => Light(),
        AppTheme.Dark => Dark(),
        AppTheme.Blue => Blue(),
        AppTheme.Midnight => Midnight(),
        _ => Light(),
    };

    public static IEnumerable<AppTheme> All => Enum.GetValues<AppTheme>();

    private static AppPalette Light() => new()
    {
        WindowBackground = Rgb(0xF5, 0xF6, 0xF8),
        PanelBackground = Rgb(0xFF, 0xFF, 0xFF),
        Border = Rgb(0xCC, 0xCC, 0xCC),
        Text = Rgb(0x1A, 0x1A, 0x1A),
        Accent = Rgb(0x2E, 0x86, 0xDE),
        AlternatingRow = Rgb(0xF2, 0xF4, 0xF7),
        Up = Rgb(0x27, 0xAE, 0x60),
        Down = Rgb(0xC0, 0x39, 0x2B),
        Hover = Rgb(0xE6, 0xE9, 0xEC),
        Pressed = Rgb(0xD8, 0xDC, 0xE1),
        DisabledBackground = Rgb(0xF0, 0xF1, 0xF3),
        DisabledForeground = Rgb(0x8F, 0x8F, 0x95),
        Chart = new ChartTheme
        {
            FigureBackground = new Color(0xFF, 0xFF, 0xFF),
            AxisText = new Color(0x33, 0x33, 0x33),
            GridLine = new Color(0xD8, 0xD8, 0xD8),
            GridMinor = new Color(0xEC, 0xEC, 0xEC),
            CandleUp = new Color(0x2E, 0x9E, 0x5B),
            CandleDown = new Color(0xC0, 0x39, 0x2B),
        },
    };

    private static AppPalette Dark() => new()
    {
        WindowBackground = Rgb(0x1E, 0x1E, 0x1E),
        PanelBackground = Rgb(0x2D, 0x2D, 0x30),
        Border = Rgb(0x3F, 0x3F, 0x46),
        Text = Rgb(0xE8, 0xE8, 0xE8),
        Accent = Rgb(0x4F, 0xC3, 0xF7),
        AlternatingRow = Rgb(0x25, 0x25, 0x26),
        Up = Rgb(0x69, 0xF0, 0xAE),
        Down = Rgb(0xFF, 0x6B, 0x6B),
        Hover = Rgb(0x3A, 0x3A, 0x40),
        Pressed = Rgb(0x46, 0x46, 0x4D),
        DisabledBackground = Rgb(0x26, 0x26, 0x2A),
        DisabledForeground = Rgb(0x7A, 0x7A, 0x82),
        Chart = new ChartTheme
        {
            FigureBackground = new Color(0x1E, 0x1E, 0x1E),
            AxisText = new Color(0xE0, 0xE0, 0xE0),
            GridLine = new Color(0x3A, 0x3A, 0x3A),
            GridMinor = new Color(0x2A, 0x2A, 0x2A),
            CandleUp = new Color(0x26, 0xA6, 0x9A),
            CandleDown = new Color(0xEF, 0x53, 0x50),
        },
    };

    private static AppPalette Blue() => new()
    {
        WindowBackground = Rgb(0x0F, 0x1B, 0x2B),
        PanelBackground = Rgb(0x16, 0x26, 0x3B),
        Border = Rgb(0x2A, 0x4A, 0x6B),
        Text = Rgb(0xD6, 0xE4, 0xF0),
        Accent = Rgb(0x4F, 0xC3, 0xF7),
        AlternatingRow = Rgb(0x13, 0x20, 0x30),
        Up = Rgb(0x69, 0xF0, 0xAE),
        Down = Rgb(0xFF, 0x8A, 0x80),
        Hover = Rgb(0x1E, 0x34, 0x50),
        Pressed = Rgb(0x28, 0x43, 0x5F),
        DisabledBackground = Rgb(0x13, 0x20, 0x30),
        DisabledForeground = Rgb(0x5A, 0x71, 0x8A),
        Chart = new ChartTheme
        {
            FigureBackground = new Color(0x0F, 0x1B, 0x2B),
            AxisText = new Color(0xC5, 0xD5, 0xE5),
            GridLine = new Color(0x1E, 0x34, 0x50),
            GridMinor = new Color(0x15, 0x28, 0x3F),
            CandleUp = new Color(0x4F, 0xC3, 0xF7),
            CandleDown = new Color(0xFF, 0x8A, 0x65),
        },
    };

    private static AppPalette Midnight() => new()
    {
        WindowBackground = Rgb(0x0B, 0x0B, 0x0F),
        PanelBackground = Rgb(0x15, 0x15, 0x1C),
        Border = Rgb(0x26, 0x26, 0x2E),
        Text = Rgb(0xC8, 0xC8, 0xD0),
        Accent = Rgb(0xBB, 0x86, 0xFC),
        AlternatingRow = Rgb(0x10, 0x10, 0x17),
        Up = Rgb(0x69, 0xF0, 0xAE),
        Down = Rgb(0xFF, 0x8A, 0x80),
        Hover = Rgb(0x1E, 0x1E, 0x2A),
        Pressed = Rgb(0x28, 0x28, 0x36),
        DisabledBackground = Rgb(0x10, 0x10, 0x18),
        DisabledForeground = Rgb(0x5E, 0x5E, 0x6E),
        Chart = new ChartTheme
        {
            FigureBackground = new Color(0x0B, 0x0B, 0x0F),
            AxisText = new Color(0x99, 0x99, 0xAA),
            GridLine = new Color(0x23, 0x23, 0x2C),
            GridMinor = new Color(0x18, 0x18, 0x20),
            CandleUp = new Color(0x00, 0xE6, 0x76),
            CandleDown = new Color(0xFF, 0x52, 0x52),
        },
    };

    private static System.Windows.Media.Color Rgb(byte r, byte g, byte b)
        => System.Windows.Media.Color.FromRgb(r, g, b);
}
