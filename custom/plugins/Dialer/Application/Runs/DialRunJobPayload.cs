namespace Callora.Plugins.Dialer.Application.Runs;

/// <summary>
/// Job payload for one dial run executed via the host job queue.
/// </summary>
public sealed record DialRunJobPayload(
    string RunId,
    string WorkspaceKey,
    int CallTimeoutSeconds);
