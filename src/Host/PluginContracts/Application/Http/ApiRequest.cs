using System.Security.Claims;

namespace Callora.Host.PluginContracts.Application.Http;

/// <summary>
/// Transport-neutral request context handed to plugin controller actions.
/// The host provides the implementation; contracts stay free of ASP.NET
/// references.
/// </summary>
public abstract class ApiRequest
{
    /// <summary>Authenticated caller.</summary>
    public abstract ClaimsPrincipal User { get; }

    /// <summary>Route parameter values by template name.</summary>
    public abstract IReadOnlyDictionary<string, string> RouteValues { get; }

    /// <summary>First value per query parameter.</summary>
    public abstract IReadOnlyDictionary<string, string> Query { get; }

    /// <summary>
    /// Workspace resolved from the request (workspaceKey query/route value);
    /// null on admin-scoped routes without workspace context.
    /// </summary>
    public abstract string? WorkspaceKey { get; }

    /// <summary>Deserializes the JSON request body; null on empty bodies.</summary>
    public abstract Task<T?> ReadJsonAsync<T>(CancellationToken cancellationToken = default);
}
