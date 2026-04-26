using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Application.Abstractions.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Callora.Host.Backend.Infrastructure.Security;

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
                    if (options.EnableBootstrapApiKeys &&
                        context.Request.Headers.ContainsKey(options.ApiKeyHeaderName))
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
                .Build());

        return services;
    }
}
