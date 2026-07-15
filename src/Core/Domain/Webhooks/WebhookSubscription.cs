namespace Callora.Core.Domain.Webhooks;

/// <summary>
/// One outbound webhook subscription: platform events matching the filter are
/// delivered to the target URL as signed HTTP POSTs.
/// </summary>
public sealed class WebhookSubscription
{
    public Guid Id { get; set; }

    /// <summary>Null subscribes across all workspaces (operator webhooks).</summary>
    public string? WorkspaceKey { get; set; }

    /// <summary>Event name filter, e.g. "call.ringing"; "*" matches all events.</summary>
    public string EventName { get; set; } = string.Empty;

    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>Shared secret for the HMAC-SHA256 payload signature.</summary>
    public string Secret { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Opt-in for unmasked payloads: by default phone numbers, display names
    /// and e-mail addresses are masked before leaving the platform
    /// (data minimization, PLAT-244).
    /// </summary>
    public bool IncludeSensitiveData { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
