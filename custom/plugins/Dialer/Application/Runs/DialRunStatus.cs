namespace Callora.Plugins.Dialer.Application.Runs;

/// <summary>
/// Lifecycle status of one dial run.
/// </summary>
public enum DialRunStatus
{
    Running = 0,
    Completed = 1,
    Failed = 2,
}
