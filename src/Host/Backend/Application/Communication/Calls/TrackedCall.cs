using Callora.Contracts.Communication;

namespace Callora.Host.Backend.Application.Communication.Calls;

/// <summary>
/// One live call held by the <see cref="ActiveCallRegistry"/>, pairing the
/// contract call handle with its workspace and channel origin.
/// </summary>
public sealed class TrackedCall(
    string workspaceKey,
    string channelId,
    ICall call,
    DateTimeOffset startedAtUtc)
{
    public string WorkspaceKey { get; } = workspaceKey;

    public string ChannelId { get; } = channelId;

    public ICall Call { get; } = call;

    public DateTimeOffset StartedAtUtc { get; } = startedAtUtc;

    public ActiveCallSnapshot ToSnapshot() => new(
        Call.CallId,
        WorkspaceKey,
        ChannelId,
        Call.Direction.ToString(),
        Call.State.ToString(),
        Call.Target.Value,
        Call.Target.DisplayName,
        StartedAtUtc);
}
