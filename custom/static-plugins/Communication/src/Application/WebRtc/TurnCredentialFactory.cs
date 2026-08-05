using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Callora.Plugin.Communication.Application.WebRtc;

/// <summary>
/// Derives the ICE server list a browser peer receives, issuing short-lived TURN credentials where
/// the deployment configured a shared secret (#114).
/// </summary>
/// <remarks>
/// The scheme is the TURN REST API that coturn and every managed TURN service implement: the
/// username is <c>{expiryUnixSeconds}:{identity}</c> and the credential is the base64 HMAC-SHA1 of
/// that username under the server's shared secret. The TURN server recomputes both and rejects an
/// expired username, so no state is exchanged and no long-lived password ever reaches a browser.
/// <para>
/// HMAC-SHA1 is not a choice here — it is what the scheme specifies and what TURN servers verify
/// against. It is a message authentication code over a non-secret string, where SHA-1's collision
/// weakness does not apply.
/// </para>
/// </remarks>
public static class TurnCredentialFactory
{
    /// <summary>
    /// Builds the browser-facing ICE server list. Servers with a shared secret get a credential
    /// valid until <c>now + CredentialTimeToLive</c>; the rest are passed through as configured.
    /// </summary>
    /// <param name="options">The deployment's ICE settings.</param>
    /// <param name="identity">
    /// Identity embedded in the username. Not a secret and not authentication — TURN servers treat
    /// it as an opaque label — but it makes a relay session traceable to the workspace that opened it.
    /// </param>
    /// <param name="now">Current time; the expiry is derived from it.</param>
    public static IReadOnlyList<IceServerView> Build(IceConfigurationOptions options, string identity, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);

        var views = new List<IceServerView>(options.Servers.Count);
        foreach (var server in options.Servers)
        {
            if (!server.IssuesShortLivedCredentials)
            {
                views.Add(new IceServerView([server.Url], server.Username, server.Credential));
                continue;
            }

            var expiry = now.Add(options.CredentialTimeToLive).ToUnixTimeSeconds();
            var username = string.Create(
                CultureInfo.InvariantCulture, $"{expiry}:{Sanitize(identity)}");
            views.Add(new IceServerView([server.Url], username, Sign(username, server.SharedSecret!)));
        }

        return views;
    }

    // The colon separates expiry from identity in the username, so it must not appear inside the
    // identity itself — a workspace key containing one would shift the expiry the server parses.
    private static string Sanitize(string identity) => identity.Replace(':', '-');

    private static string Sign(string username, string sharedSecret)
    {
        var key = Encoding.UTF8.GetBytes(sharedSecret);
#pragma warning disable CA5350 // The TURN REST API defines HMAC-SHA1; the server verifies nothing else.
        var mac = HMACSHA1.HashData(key, Encoding.UTF8.GetBytes(username));
#pragma warning restore CA5350
        return Convert.ToBase64String(mac);
    }
}
