namespace Callora.Administration.Api;

/// <summary>
/// Public shape of one webhook subscription; the secret never leaves the API.
/// </summary>
/// <remarks>
/// <see cref="MatchesKnownEvent"/> is derived per request, never stored. A subscription may
/// legitimately be ahead of its plugin — <c>communication.call.ringing</c> before the plugin
/// is installed is the normal order when preparing a workspace — while a misspelling never
/// fires at all. Without this flag the two look identical. Because it is derived, it becomes
/// true on its own once the plugin arrives.
/// </remarks>
public sealed record WebhookSubscriptionApiResponse(
    Guid Id,
    string? WorkspaceKey,
    string EventName,
    string TargetUrl,
    bool IsActive,
    bool IncludeSensitiveData,
    DateTimeOffset CreatedAtUtc,
    bool MatchesKnownEvent);
