using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Tests.Support;

/// <summary>
/// The <see cref="ILogger"/> handed out by <see cref="CapturingLoggerFactory"/>: writes formatted
/// messages into the shared queue that gives the factory its ordering.
/// </summary>
internal sealed class CapturingCategoryLogger(ConcurrentQueue<string> entries) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        entries.Enqueue(formatter(state, exception));
    }
}
