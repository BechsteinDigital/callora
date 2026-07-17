using Callora.Core.Application.Integrations;
using Callora.Core.Application.Policies;
using Callora.Core.Extensibility;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

namespace Callora.Core.Infrastructure.Security;

[CalloraInternal("API-key authentication handler — not a plugin contract (REV2 §7.2)")]
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    BackendHostOptions hostOptions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(hostOptions.ApiKeyHeaderName, out var providedValues))
        {
            return AuthenticateResult.NoResult();
        }

        var providedKey = providedValues.ToString();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return AuthenticateResult.Fail($"Missing '{hostOptions.ApiKeyHeaderName}' header.");
        }

        // 1) Named integration (PLAT-264): a hashed lookup resolves the credential
        // to its own RBAC role and scope — never platform super-admin.
        var integrationStore = Context.RequestServices.GetService<IIntegrationCredentialStore>();
        if (integrationStore is not null)
        {
            var keyHash = IntegrationApiKey.ComputeHash(providedKey);
            var integration = await integrationStore.FindActiveByKeyHashAsync(keyHash, Context.RequestAborted)
                .ConfigureAwait(false);
            if (integration is not null)
            {
                var principal = IntegrationPrincipalFactory.Create(integration);
                return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
            }
        }

        // 2) Global bootstrap keys: the operator break-glass credential, kept for
        // first-run setup. Grants super-admin, so it stays gated behind opt-in.
        if (hostOptions.EnableBootstrapApiKeys &&
            (!hostOptions.RequireApiKeyAuthentication || IsKnownBootstrapKey(providedKey)))
        {
            var principal = CreateBootstrapPrincipal("bootstrap-api-key");
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }

        return AuthenticateResult.Fail("Invalid API key.");
    }

    private bool IsKnownBootstrapKey(string provided)
    {
        if (string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        foreach (var key in hostOptions.ApiKeys ?? [])
        {
            if (ConstantTimeEquals(key, provided))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ConstantTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static ClaimsPrincipal CreateBootstrapPrincipal(string identityName)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, identityName),
                new Claim(ClaimTypes.Role, BackendRoles.HostApi),
                new Claim(ClaimTypes.Role, BackendRoles.SuperAdmin),
                new Claim(BackendClaimTypes.CalloraScope, BackendAuthScopes.Platform),
                new Claim(BackendClaimTypes.Permission, "*")
            ],
            authenticationType: ApiKeyAuthenticationDefaults.Scheme);

        return new ClaimsPrincipal(identity);
    }
}
