namespace Callora.Plugins.Dialer.Application.Runs;

/// <summary>
/// Options for one dial run.
/// </summary>
public sealed record DialRunOptions(TimeSpan CallTimeout)
{
    /// <summary>
    /// Default options: 30 seconds per call.
    /// </summary>
    public static DialRunOptions Default { get; } = new(TimeSpan.FromSeconds(30));
}
