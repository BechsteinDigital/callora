namespace Callora.Host.Backend.Api;

/// <summary>
/// Request body for placing one outbound call. The workspace is passed as
/// query parameter so workspace-scope authorization can evaluate it. Without
/// a channel identifier the first voice channel of the workspace is used.
/// </summary>
public sealed record PlaceCallRequest(
    string Target,
    string? TargetDisplayName = null,
    string? ChannelId = null);
