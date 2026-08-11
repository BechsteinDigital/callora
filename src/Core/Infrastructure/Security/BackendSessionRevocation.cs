using Callora.Core.Application.Security;
using Callora.Core.Extensibility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Revokes the session a request presented (#105). Reads the token off the request
/// rather than the principal, so it also works on an anonymous endpoint such as
/// logout — where authentication may already have been skipped.
/// </summary>
[CalloraInternal("Session revocation helper — not a plugin contract (REV2 §7.2)")]
public static class BackendSessionRevocation
{
    /// <summary>
    /// Marks the presented session revoked until its token expires. A missing,
    /// malformed, unverifiable or already-expired token is a no-op: there is nothing
    /// left to revoke, and logout must never fail because of it.
    /// <para>
    /// <b>Geprüft, nicht nur gelesen.</b> Vorher genügte hier ein selbst gebautes JWT: jti und exp
    /// wurden geparst und direkt in die Revocation-Tabelle geschrieben. Mit einem exp weit in der
    /// Zukunft löschte <c>PurgeExpiredAsync</c> die Zeile nie — der anonyme Endpunkt war damit ein
    /// unauthentifizierter Schreibzugriff auf eine wachsende Tabelle. Ein fremdes Logout war es
    /// nicht (jti ist eine Guid und nur mit dem Token selbst bekannt), aber Speicher schon.
    /// </para>
    /// </summary>
    public static async Task RevokeCurrentSessionAsync(
        HttpContext httpContext,
        IBackendSessionRevocationStore revocationStore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(revocationStore);

        // Zuerst der Principal, den die Pipeline schon geprüft hat. Anonym heißt nicht ungeprüft:
        // Läuft UseAuthentication vor diesem Endpunkt, sind Signatur, Aussteller und Laufzeit
        // bereits validiert — bei einem externen IdP auch gegen dessen Metadaten, deren Schlüssel
        // hier gar nicht vorliegt. Dieser Zweig ist deshalb der einzige, der unter OidcAuthority
        // überhaupt etwas revozieren kann.
        if (TryReadSession(httpContext.User, out var session))
        {
            await revocationStore.RevokeAsync(session.TokenId, session.ExpiresAtUtc, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        // Ohne authentifizierten Principal wird selbst geprüft. Das ist kein toter Zweig: Der
        // Endpunkt liest den Token vom Request, damit er auch dort wirkt, wo die
        // Authentifizierung übersprungen wurde — und er greift, wenn die Pipeline die Session
        // serverseitig abgelehnt hat (Passwortwechsel, Deaktivierung, schon revoziert), das
        // Token selbst aber noch echt und gültig ist.
        var token = ReadToken(httpContext);
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var validated = Validate(httpContext, token);
        if (validated is null || !TryReadSession(validated, out var verified))
        {
            return;
        }

        await revocationStore.RevokeAsync(verified.TokenId, verified.ExpiresAtUtc, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Die Sitzung hinter einem geprüften Principal: ihre <c>jti</c> und wie lange sie noch läuft.
    /// Fehlt eines von beidem oder ist der Ablauf vorbei, gibt es nichts zu revozieren.
    /// </summary>
    private static bool TryReadSession(
        ClaimsPrincipal? principal,
        out (string TokenId, DateTimeOffset ExpiresAtUtc) session)
    {
        session = default;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var tokenId = principal.FindFirstValue(BackendClaimTypes.TokenId);
        if (string.IsNullOrWhiteSpace(tokenId))
        {
            return false;
        }

        var expiry = principal.FindFirstValue(JwtRegisteredClaimNames.Exp);
        if (!long.TryParse(expiry, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            return false;
        }

        var expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        if (expiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        session = (tokenId.Trim(), expiresAtUtc);
        return true;
    }

    /// <summary>
    /// Prüft den Token mit den Parametern, die der Host selbst verwendet — nicht mit einer
    /// zweiten, hier nachgebauten Wahrheit. Sind keine konfiguriert, gibt es keine
    /// JWT-Authentifizierung in diesem Host und damit auch nichts, was diese Tabelle bedeutete.
    /// </summary>
    private static ClaimsPrincipal? Validate(HttpContext httpContext, string token)
    {
        var parameters = httpContext.RequestServices
            .GetService<IOptionsMonitor<JwtBearerOptions>>()
            ?.Get(JwtBearerDefaults.AuthenticationScheme)
            ?.TokenValidationParameters;
        if (parameters is null)
        {
            return null;
        }

        try
        {
            return new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            // Strukturell kein Token — dieselbe Antwort wie eine gescheiterte Prüfung.
            return null;
        }
    }

    private static string? ReadToken(HttpContext httpContext)
    {
        var authorization = httpContext.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        var options = httpContext.RequestServices
            .GetService(typeof(Callora.Core.Application.Policies.BackendHostOptions))
            as Callora.Core.Application.Policies.BackendHostOptions;
        if (options is null)
        {
            return null;
        }

        return httpContext.Request.Cookies.TryGetValue(
            BackendAuthCookieService.ResolveCookieName(options), out var cookieToken)
            ? cookieToken
            : null;
    }
}
