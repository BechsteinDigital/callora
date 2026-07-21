using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// One live call held by the <see cref="VoipCallHub"/>, pairing the contract
/// call handle with its workspace and channel origin.
/// </summary>
public sealed class VoipTrackedCall(
    string workspaceKey,
    string channelId,
    ICall call,
    DateTimeOffset startedAtUtc)
{
    public string WorkspaceKey { get; } = workspaceKey;

    public string ChannelId { get; } = channelId;

    public ICall Call { get; } = call;

    public DateTimeOffset StartedAtUtc { get; } = startedAtUtc;

    /// <summary>
    /// Event handlers attached to <see cref="Call"/>, held so the hub can
    /// detach every one of them on termination — otherwise a long-lived call
    /// object keeps the tracked entry (and the hub) alive (audit finding H5).
    /// </summary>
    public EventHandler<CallStateChangedEventArgs>? StateChangedHandler { get; set; }

    public EventHandler<RecordingConsentChangedEventArgs>? ConsentChangedHandler { get; set; }

    public CallSummary ToSummary() => new(
        Call.CallId,
        WorkspaceKey,
        ChannelId,
        Call.Direction.ToString(),
        Call.State.ToString(),
        Call.Target.Value,
        Call.Target.DisplayName,
        StartedAtUtc);
}
