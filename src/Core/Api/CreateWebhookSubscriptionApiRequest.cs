namespace Callora.Core.Api;

public sealed record CreateWebhookSubscriptionApiRequest(
    string EventName,
    string TargetUrl,
    string Secret,
    string? WorkspaceKey,
    bool IncludeSensitiveData = false);
