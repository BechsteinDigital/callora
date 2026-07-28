namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Request to place one outbound call. The channel is chosen by <see cref="ChannelId"/> when given,
/// otherwise the first voice-capable channel registered for the workspace is used.
/// </summary>
/// <param name="WorkspaceKey">Workspace that owns the call.</param>
/// <param name="To">Channel-neutral target address (e.g. a phone number "+49301234567").</param>
/// <param name="ChannelId">Optional explicit channel; null picks the first voice-capable channel.</param>
/// <param name="DisplayName">Optional human-readable name for the remote party.</param>
public sealed record PlaceCallCommand(
    string WorkspaceKey,
    string To,
    string? ChannelId = null,
    string? DisplayName = null);
