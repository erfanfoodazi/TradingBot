namespace Trading.Infrastructure.Options;

/// <summary>
/// Configuration for the Python connector backend.
/// Bound from the "PythonApi" section of appsettings.json.
/// </summary>
public sealed class PythonApiOptions
{
    public const string SectionName = "PythonApi";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8000";

    public string WebSocketUrl { get; set; } = "ws://127.0.0.1:8000";

    public string? ApiKey { get; set; }

    public int ReconnectDelayMs { get; set; } = 3000;

    /// <summary>Whether the WPF app should launch the Python API on startup.</summary>
    public bool AutoStartPython { get; set; } = true;

    /// <summary>Python interpreter used to launch the backend.</summary>
    public string PythonExecutable { get; set; } = "python";

    /// <summary>
    /// Directory containing the Python backend. When empty, the app searches
    /// for the "Trading.Python" folder relative to the app output directory.
    /// </summary>
    public string? PythonWorkingDirectory { get; set; }

    /// <summary>Host/port passed to uvicorn. Defaults match <see cref="BaseUrl"/>.</summary>
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 8000;
}