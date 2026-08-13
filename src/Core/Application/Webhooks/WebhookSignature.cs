using System.Security.Cryptography;
using System.Text;

namespace Callora.Core.Application.Webhooks;

/// <summary>
/// HMAC-SHA256 payload signature carried in the X-Callora-Signature header so
/// receivers can verify origin and integrity.
/// </summary>
public static class WebhookSignature
{
    public const string HeaderName = "X-Callora-Signature";
    public const string EventHeaderName = "X-Callora-Event";

    /// <summary>
    /// Stable id of one delivery, identical across every retry of that delivery — the value a
    /// receiver deduplicates on.
    /// </summary>
    /// <remarks>
    /// Not signed, and deliberately so: the signature covers the body, and the id belongs to the
    /// delivery attempt rather than to the event. Same split as GitHub's X-GitHub-Delivery. A
    /// forged id needs a man in the middle, who could drop the request outright — dedup is not the
    /// weak point there.
    /// </remarks>
    public const string DeliveryHeaderName = "X-Callora-Delivery";

    public static string Compute(string secret, string body)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(body);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return "sha256=" + Convert.ToHexStringLower(hash);
    }
}
