using System.Security.Claims;
using Callora.Core.Domain.Integrations;
using Callora.Core.Extensibility;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Builds the <see cref="ClaimsPrincipal"/> for an authenticated integration
/// (PLAT-264). Unlike the bootstrap API key, an integration receives only its
/// single assigned RBAC role and configured scope — never super-admin or a
/// wildcard permission. The role's permissions are expanded afterwards by
/// <see cref="BackendClaimsTransformation"/>.
/// </summary>
[CalloraInternal("Integration principal building — not a plugin contract (REV2 §7.2)")]
public static class IntegrationPrincipalFactory
{
    public static ClaimsPrincipal Create(IntegrationCredential integration)
    {
        ArgumentNullException.ThrowIfNull(integration);

        var identityName = $"integration:{integration.Name}";
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, identityName),
            new(ClaimTypes.NameIdentifier, identityName),
            new(ClaimTypes.Role, integration.RoleName),
            new(BackendClaimTypes.CalloraScope, integration.Scope)
        };

        if (!string.IsNullOrWhiteSpace(integration.WorkspaceKey))
            claims.Add(new Claim(BackendClaimTypes.WorkspaceKey, integration.WorkspaceKey));

        var identity = new ClaimsIdentity(claims, authenticationType: ApiKeyAuthenticationDefaults.Scheme);
        return new ClaimsPrincipal(identity);
    }
}
