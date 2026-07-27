using Callora.Core.Infrastructure.Mcp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Callora.Core.Infrastructure.DependencyInjection;

/// <summary>
/// Composition for the host's MCP framework: one Streamable-HTTP MCP server mounted at <c>/mcp</c>,
/// backed by a live tool collection that the <see cref="McpToolRegistry"/> keeps in sync with the
/// active plugin catalog. The transport shell lives in the host (the ASP.NET endpoint table freezes
/// after build, so plugins that load later cannot mount it), while the tools are contributed by
/// plugins. Authentication is an OAuth 2.1 resource server (RFC 9728): the existing operator-JWT scheme
/// validates the bearer token, while the MCP scheme owns the 401 challenge so it can point clients at
/// the protected-resource metadata; per-call RBAC is enforced inside each tool.
/// </summary>
public static class CalloraMcpCompositionExtensions
{
    /// <summary>The route pattern the MCP Streamable HTTP transport is mounted under.</summary>
    public const string McpEndpointPattern = "/mcp";

    /// <summary>The authorization policy that gates the <c>/mcp</c> mount to the MCP authentication scheme.</summary>
    public const string McpAuthorizationPolicy = "CalloraMcp";

    /// <summary>
    /// Registers the MCP server, its shared live tool collection and the <see cref="McpToolRegistry"/>,
    /// and wires the MCP resource-server authentication scheme plus its authorization policy.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="authenticationBuilder">
    /// The existing authentication builder (already carrying Callora's operator-JWT scheme); the MCP
    /// resource-metadata scheme is added to it.
    /// </param>
    /// <param name="forwardAuthenticateScheme">
    /// The existing scheme that validates the operator bearer token (Callora's composite/JWT scheme). The
    /// MCP scheme forwards authentication to it so token validation is unchanged, but keeps its own
    /// challenge so a 401 carries the <c>resource_metadata</c> pointer.
    /// </param>
    /// <param name="resource">The MCP resource URL advertised as this protected resource's identifier.</param>
    /// <param name="authorizationServer">
    /// The OAuth authorization server (token issuer) to advertise, or <see langword="null"/> when the
    /// instance runs without one (v1 default: "bring your own token"), leaving <c>authorization_servers</c> empty.
    /// </param>
    public static IServiceCollection AddCalloraMcp(
        this IServiceCollection services,
        AuthenticationBuilder authenticationBuilder,
        string forwardAuthenticateScheme,
        string resource,
        string? authorizationServer)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(authenticationBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(forwardAuthenticateScheme);
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        services.AddHttpContextAccessor();

        // The one collection instance the MCP server serves and the registry mutates. Sharing the exact
        // instance is what makes a plugin's tools live the moment it activates.
        var toolCollection = new McpServerPrimitiveCollection<McpServerTool>();
        services.AddSingleton(toolCollection);

        services
            .AddMcpServer(options =>
            {
                options.Capabilities ??= new ServerCapabilities();
                options.Capabilities.Tools ??= new ToolsCapability();
                options.Capabilities.Tools.ListChanged = true;
                options.ToolCollection = toolCollection;
            })
            .WithHttpTransport();

        services.AddSingleton(sp => new McpToolRegistry(
            toolCollection,
            sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<McpToolRegistry>>()));

        authenticationBuilder.AddMcp(options =>
        {
            // Keep the existing operator-JWT validation, but let the MCP handler emit the RFC 9728 401
            // challenge (WWW-Authenticate with resource_metadata) instead of the app-wide challenge.
            options.ForwardAuthenticate = forwardAuthenticateScheme;

            var metadata = new ProtectedResourceMetadata { Resource = resource };
            // v1 ships no own OAuth authorization server; advertise one only when configured, otherwise
            // leave authorization_servers empty (do not point it at the resource URL itself). scopes are
            // left empty: RBAC runs on permission claims, not OAuth scopes.
            if (!string.IsNullOrWhiteSpace(authorizationServer))
            {
                metadata.AuthorizationServers.Add(authorizationServer);
            }

            options.ResourceMetadata = metadata;
        });

        // Gate the /mcp mount to the MCP scheme specifically, so its handler (not the composite scheme)
        // answers an unauthenticated request with the resource-metadata challenge.
        services.AddAuthorizationBuilder()
            .AddPolicy(McpAuthorizationPolicy, policy => policy
                .AddAuthenticationSchemes(McpAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser());

        return services;
    }

    /// <summary>Mounts the MCP Streamable HTTP endpoint at <c>/mcp</c>, gated to the MCP auth scheme.</summary>
    public static IEndpointRouteBuilder MapCalloraMcp(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapMcp(McpEndpointPattern).RequireAuthorization(McpAuthorizationPolicy);
        return endpoints;
    }
}
