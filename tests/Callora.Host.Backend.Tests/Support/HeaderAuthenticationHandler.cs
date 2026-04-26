using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Callora.Host.Backend.Tests.Support;

public sealed class HeaderAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
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
        AddClaimsFromHeader(claims, "X-Test-Workspace-Key", "workspace_key");

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
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
