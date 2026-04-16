using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text;
using Callora.Host.Backend.Application.Policies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Callora.Host.Backend.Infrastructure.Security;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    BackendHostOptions hostOptions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!hostOptions.RequireApiKeyAuthentication)
        {
            var bypassPrincipal = CreatePrincipal("anonymous-host");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(bypassPrincipal, Scheme.Name)));
        }

        if (!Request.Headers.TryGetValue(hostOptions.ApiKeyHeaderName, out var providedKey))
            return Task.FromResult(AuthenticateResult.Fail($"Missing '{hostOptions.ApiKeyHeaderName}' header."));

        if (!IsKnownApiKey(providedKey.ToString()))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var principal = CreatePrincipal("host-api-key");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }

    private bool IsKnownApiKey(string provided)
    {
        if (string.IsNullOrWhiteSpace(provided))
            return false;

        foreach (var key in hostOptions.ApiKeys ?? [])
        {
            if (ConstantTimeEquals(key, provided))
                return true;
        }

        return false;
    }

    private static bool ConstantTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static ClaimsPrincipal CreatePrincipal(string identityName)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, identityName),
                new Claim(ClaimTypes.Role, "host.api")
            ],
            authenticationType: ApiKeyAuthenticationDefaults.Scheme);

        return new ClaimsPrincipal(identity);
    }
}
