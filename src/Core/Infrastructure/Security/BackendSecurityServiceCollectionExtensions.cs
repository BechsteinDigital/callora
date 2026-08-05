using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Callora.Core.Infrastructure.Security;

public static class BackendSecurityServiceCollectionExtensions
{
    private const string CompositeScheme = "CalloraComposite";

    public static IServiceCollection AddBackendApiSecurity(
        this IServiceCollection services,
        BackendHostOptions options)
    {
        if (options.EnableBootstrapApiKeys && options.RequireApiKeyAuthentication && (options.ApiKeys is null || options.ApiKeys.Length == 0))
        {
            throw new InvalidOperationException(
                "BackendHost.ApiKeys must contain at least one key when authentication is required.");
        }

        if (string.IsNullOrWhiteSpace(options.OidcAuthority) && string.IsNullOrWhiteSpace(options.JwtSigningKey))
        {
            throw new InvalidOperationException(
                "Either BackendHost.OidcAuthority or BackendHost.JwtSigningKey must be configured for JWT authentication.");
        }

        if (options.RequireExternalIdentityForOperators && string.IsNullOrWhiteSpace(options.OidcAuthority))
        {
            // Otherwise the setting would lock every operator out with no way back in.
            throw new InvalidOperationException(
                "BackendHost.RequireExternalIdentityForOperators needs BackendHost.OidcAuthority — otherwise no operator can sign in at all.");
        }

        // The repository-known dev signing key is rejected outside Development by
        // the unified BackendSecretHygiene gate in the composition root.

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IBackendRbacStore>(_ => new InMemoryBackendRbacStore(options));
        services.AddTransient<IClaimsTransformation, BackendClaimsTransformation>();

        services
            .AddAuthentication(authenticationOptions =>
            {
                authenticationOptions.DefaultAuthenticateScheme = CompositeScheme;
                authenticationOptions.DefaultChallengeScheme = CompositeScheme;
                authenticationOptions.DefaultScheme = CompositeScheme;
            })
            .AddPolicyScheme(CompositeScheme, CompositeScheme, policyOptions =>
            {
                policyOptions.ForwardDefaultSelector = context =>
                {
                    // An API-key header routes to the ApiKey handler regardless of the
                    // bootstrap configuration: named integrations authenticate here too
                    // (PLAT-264), even when bootstrap keys are disabled.
                    if (context.Request.Headers.ContainsKey(options.ApiKeyHeaderName))
                    {
                        return ApiKeyAuthenticationDefaults.Scheme;
                    }

                    return JwtBearerDefaults.AuthenticationScheme;
                };
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, jwtOptions =>
            {
                jwtOptions.RequireHttpsMetadata = false;
                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.JwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = options.JwtAudience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.JwtSigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                if (!string.IsNullOrWhiteSpace(options.OidcAuthority))
                {
                    jwtOptions.Authority = options.OidcAuthority;
                }

                jwtOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (!string.IsNullOrWhiteSpace(context.Token))
                        {
                            return Task.CompletedTask;
                        }

                        var cookieName = BackendAuthCookieService.ResolveCookieName(options);
                        if (context.Request.Cookies.TryGetValue(cookieName, out var token) &&
                            !string.IsNullOrWhiteSpace(token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                    // A valid signature is not enough: the session must still exist
                    // (#105). Password changes, deactivation, deletion, RBAC changes
                    // and logout all invalidate an otherwise-valid token here.
                    OnTokenValidated = async context =>
                    {
                        var validator = context.HttpContext.RequestServices
                            .GetService<IBackendSessionValidator>();
                        if (validator is null)
                        {
                            // Composition always registers one; a host without local
                            // accounts (pure OIDC) has nothing to revoke.
                            return;
                        }

                        var reason = await validator
                            .ValidateAsync(context.Principal!, context.HttpContext.RequestAborted)
                            .ConfigureAwait(false);
                        if (reason is not null)
                        {
                            context.Fail(reason);
                        }
                    }
                };
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.Scheme,
                _ => { });

        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(CompositeScheme)
                .RequireAuthenticatedUser()
                .Build())
            // Fail-closed Sicherheitsnetz (PLAT-267): eine Route ohne explizite
            // Autorisierung ist nicht offen, sondern verlangt Authentifizierung.
            // Bewusst anonyme Routen sind mit AllowAnonymous markiert und bleiben
            // davon ausgenommen.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(CompositeScheme)
                .RequireAuthenticatedUser()
                .Build());

        return services;
    }
}
