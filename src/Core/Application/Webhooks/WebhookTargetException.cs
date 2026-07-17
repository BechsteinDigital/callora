namespace Callora.Core.Application.Webhooks;

/// <summary>
/// A webhook target was rejected — an unsupported scheme, or an address blocked by the SSRF
/// egress policy. Both are caller-facing validation faults mapping to HTTP 400.
/// </summary>
public sealed class WebhookTargetException : CalloraException
{
    private const int BadRequest = 400;

    private WebhookTargetException(string errorCode, string message)
        : base(errorCode, BadRequest, message)
    {
    }

    /// <summary>Error code for a webhook target that does not use http or https.</summary>
    public const string InvalidSchemeCode = "WEBHOOK__INVALID_SCHEME";

    /// <summary>Error code for a target blocked by the egress (SSRF) policy.</summary>
    public const string TargetBlockedCode = "WEBHOOK__TARGET_BLOCKED";

    /// <summary>The target uses a scheme other than http/https.</summary>
    public static WebhookTargetException InvalidScheme() =>
        new(InvalidSchemeCode, "Webhook targets must use http(s).");

    /// <summary>The target resolves to a non-public address and was blocked by the SSRF guard.</summary>
    /// <param name="host">The rejected target host.</param>
    public static WebhookTargetException Blocked(string host) =>
        new(TargetBlockedCode, $"Webhook target '{host}' resolves to a non-public address and was blocked.");
}
