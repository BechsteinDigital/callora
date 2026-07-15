namespace Callora.Administration.Api;

/// <summary>
/// Public shape of one webhook subscription; the secret never leaves the API.
/// </summary>
public sealed record WebhookSubscriptionApiResponse(
    Guid Id,
    string? WorkspaceKey,
    string EventName,
    string TargetUrl,
    bool IsActive,
    bool IncludeSensitiveData,
    DateTimeOffset CreatedAtUtc);
