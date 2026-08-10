namespace Trading.Shared.Responses;

public interface IApiResponse
{
    bool Success { get; set; }

    string? Message { get; set; }
}