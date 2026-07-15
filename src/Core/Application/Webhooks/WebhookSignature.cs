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

    public static string Compute(string secret, string body)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(body);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return "sha256=" + Convert.ToHexStringLower(hash);
    }
}
