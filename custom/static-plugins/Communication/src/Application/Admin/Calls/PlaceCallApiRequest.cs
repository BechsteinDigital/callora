namespace Callora.Plugin.Communication.Application.Admin.Calls;

/// <summary>
/// Request body for <c>POST calls</c>. The workspace is never taken from the body — it is the caller's
/// authoritative scope.
/// </summary>
/// <param name="To">Channel-neutral target address (e.g. "+49301234567"). Required.</param>
/// <param name="ChannelId">Optional explicit channel; omitted picks the first voice-capable channel.</param>
/// <param name="DisplayName">Optional human-readable name for the remote party.</param>
public sealed record PlaceCallApiRequest(
    string? To,
    string? ChannelId = null,
    string? DisplayName = null);
