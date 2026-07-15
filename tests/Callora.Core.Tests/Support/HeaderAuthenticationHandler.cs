using System.Security.Claims;
using System.Text.Encodings.Web;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Callora.Core.Tests.Support;

public sealed class HeaderAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>
    /// X-Test-Callora-Scope value that suppresses the scope claim entirely,
    /// simulating a legacy/unscoped principal for fail-closed tests.
    /// </summary>
    public const string NoScope = "none";

    public HeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "header-user")
        };

        AddClaimsFromHeader(claims, "X-Test-Roles", ClaimTypes.Role);
        AddClaimsFromHeader(claims, "X-Test-Permissions", "permission");
        AddClaimsFromHeader(claims, "X-Test-Scopes", "scope");
        AddClaimsFromHeader(claims, "X-Test-Workspace-Key", BackendClaimTypes.WorkspaceKey);

        AddCalloraScopeClaim(claims);

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private void AddCalloraScopeClaim(List<Claim> claims)
    {
        if (Request.Headers.TryGetValue("X-Test-Callora-Scope", out var explicitScope))
        {
            var value = explicitScope.ToString().Trim();
            if (!string.Equals(value, NoScope, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(value))
            {
                claims.Add(new Claim(BackendClaimTypes.CalloraScope, value));
            }

            return;
        }

        var scope = claims.Any(x => x.Type == BackendClaimTypes.WorkspaceKey)
            ? BackendAuthScopes.Workspace
            : BackendAuthScopes.Platform;
        claims.Add(new Claim(BackendClaimTypes.CalloraScope, scope));
    }

    private void AddClaimsFromHeader(List<Claim> claims, string headerName, string claimType)
    {
        if (!Request.Headers.TryGetValue(headerName, out var values))
        {
            return;
        }

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new Claim(claimType, part));
            }
        }
    }
}
