namespace Trading.Shared.Requests;

public sealed class StrategyRequestDto
{
    public int? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ParametersJson { get; set; } = "{}";

    public bool IsActive { get; set; }
}