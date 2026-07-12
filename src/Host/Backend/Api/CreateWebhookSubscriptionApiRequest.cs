namespace Callora.Host.Backend.Api;

public sealed record CreateWebhookSubscriptionApiRequest(
    string EventName,
    string TargetUrl,
    string Secret,
    string? WorkspaceKey);
