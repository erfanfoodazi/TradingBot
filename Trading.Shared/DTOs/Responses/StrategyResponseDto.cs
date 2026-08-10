namespace Trading.Shared.Responses;

public sealed class StrategyResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ParametersJson { get; set; } = "{}";

    public bool IsActive { get; set; }
}