using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Tests.Support;

/// <summary>
/// An <see cref="ILoggerFactory"/> that funnels every category into one ordered list, so a test can
/// assert on what happened <i>and in which order</i> across components that never see each other.
/// </summary>
/// <remarks>
/// Its reason for existing beyond convenience: logging is one of the few surfaces a plugin can reach
/// through the curated service provider, which makes it the natural channel for a plugin fixture to
/// report back across a load-context boundary.
/// </remarks>
public sealed class CapturingLoggerFactory : ILoggerFactory
{
    /// <summary>Every entry logged through this factory, in the order it was written.</summary>
    public ConcurrentQueue<string> Entries { get; } = new();

    /// <summary>The messages only, which is what an ordering assertion usually compares against.</summary>
    public IReadOnlyList<string> Messages => [.. Entries];

    public ILogger CreateLogger(string categoryName) => new CapturingCategoryLogger(Entries);

    public void AddProvider(ILoggerProvider provider)
    {
        // Nothing to forward to: this factory is the sink.
    }

    public void Dispose()
    {
        // Nothing to release.
    }
}
