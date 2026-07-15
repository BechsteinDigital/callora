namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Live-call surface exported by communication plugins (PLAT-257). The host
/// carries no call logic of its own: its /api/calls facade aggregates the
/// exported directories of all active communication plugins.
/// </summary>
public interface ICallDirectory
{
    /// <summary>Live calls of one workspace.</summary>
    IReadOnlyList<CallSummary> List(string workspaceKey);

    /// <summary>
    /// Resolves one live call scoped to its workspace; false when unknown
    /// or owned by another workspace.
    /// </summary>
    bool TryGet(string workspaceKey, string callId, out ICall? call);

    /// <summary>
    /// Places an outbound call over a voice channel of the workspace.
    /// Throws <see cref="InvalidOperationException"/> when no matching
    /// channel is registered.
    /// </summary>
    Task<CallSummary> PlaceCallAsync(
        string workspaceKey,
        string? channelId,
        CallTarget target,
        CancellationToken cancellationToken = default);
}
