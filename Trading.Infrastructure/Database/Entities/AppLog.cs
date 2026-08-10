namespace Trading.Infrastructure.Database.Entities;

public sealed class AppLog
{
    public long Id { get; set; }

    public string Level { get; set; } = "Information";

    public string Component { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? StackTrace { get; set; }

    public long? Login { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}