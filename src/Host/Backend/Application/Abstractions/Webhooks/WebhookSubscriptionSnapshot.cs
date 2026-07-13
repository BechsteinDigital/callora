namespace Callora.Host.Backend.Application.Abstractions.Webhooks;

public sealed record WebhookSubscriptionSnapshot(
    Guid Id,
    string? WorkspaceKey,
    string EventName,
    string TargetUrl,
    string Secret,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    bool IncludeSensitiveData = false);
