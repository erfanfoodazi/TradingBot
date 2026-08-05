namespace Trading.Shared.Responses;

public sealed class ErrorResponseDto
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}