using Callora.Host.Backend.Application.Policies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Callora.Host.Backend.Infrastructure.Security;

public static class BackendSecurityServiceCollectionExtensions
{
    public static IServiceCollection AddBackendApiSecurity(
        this IServiceCollection services,
        BackendHostOptions options)
    {
        if (options.RequireApiKeyAuthentication && (options.ApiKeys is null || options.ApiKeys.Length == 0))
        {
            throw new InvalidOperationException(
                "BackendHost.ApiKeys must contain at least one key when authentication is required.");
        }

        services
            .AddAuthentication(ApiKeyAuthenticationDefaults.Scheme)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.Scheme,
                _ => { });

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(ApiKeyAuthenticationDefaults.Scheme)
                .RequireAuthenticatedUser()
                .Build());

        return services;
    }
}
