using Microsoft.Extensions.Logging;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Minimal <see cref="ILogger{T}"/> that records emitted entries so tests can
/// assert on log output (e.g. a warning) without a mocking framework.
/// </summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception)));
    }
}
