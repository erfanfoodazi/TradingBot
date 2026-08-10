namespace Trading.Core.Exceptions;

/// <summary>
/// Represents a failure when communicating with the Python backend.
/// </summary>
public sealed class PythonApiException : Exception
{
    public string? Code { get; }

    public PythonApiException(string message, string? code = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }
}