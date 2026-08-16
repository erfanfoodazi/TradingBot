using Trading.Core.Indicators;

namespace TradingBot.UI.Models;

/// <summary>
/// An entry in the toolbar's indicator dropdown. A null <see cref="Type"/>
/// represents "None" (no indicator applied to the chart).
/// </summary>
public sealed class IndicatorOption
{
    public IndicatorType? Type { get; }

    public string Name { get; }

    public IndicatorOption(IndicatorType? type, string name)
    {
        Type = type;
        Name = name;
    }

    public override string ToString() => Name;
}