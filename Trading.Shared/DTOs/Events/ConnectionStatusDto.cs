namespace Trading.Shared.Events;

public sealed class ConnectionStatusDto
{
    public bool Connected { get; set; }

    public DateTime Time { get; set; }

    public string Message { get; set; } = string.Empty;
}