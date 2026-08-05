using Callora.Core.Application.Security;
using Callora.Core.Extensibility;
using System.IdentityModel.Tokens.Jwt;

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
    /// malformed or already-expired token is a no-op: there is nothing left to
    /// revoke, and logout must never fail because of it.
    /// </summary>
    public static async Task RevokeCurrentSessionAsync(
        HttpContext httpContext,
        IBackendSessionRevocationStore revocationStore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(revocationStore);

        var token = ReadToken(httpContext);
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        JwtSecurityToken parsed;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
            {
                return;
            }

            parsed = handler.ReadJwtToken(token);
        }
        catch (ArgumentException)
        {
            return;
        }

        var tokenId = parsed.Claims.FirstOrDefault(x => x.Type == BackendClaimTypes.TokenId)?.Value;
        if (string.IsNullOrWhiteSpace(tokenId))
        {
            return;
        }

        var expiresAtUtc = new DateTimeOffset(parsed.ValidTo, TimeSpan.Zero);
        if (expiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return;
        }

        await revocationStore.RevokeAsync(tokenId, expiresAtUtc, cancellationToken).ConfigureAwait(false);
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
