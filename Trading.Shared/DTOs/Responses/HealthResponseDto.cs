namespace Trading.Shared.Responses;

public sealed class HealthResponseDto
{
    public bool Connected { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Server { get; set; }

    public long? Login { get; set; }
}