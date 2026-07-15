using Callora.Workspace.Api;

namespace Callora.Workspace;

/// <summary>
/// Composition entry for the Workspace module — the tenant-facing storefront
/// surface (public/anonymous pages plus the workspace-scoped theme endpoint,
/// analogous to Shopware's Storefront). The distribution skeleton calls this;
/// Core never references Workspace.
/// </summary>
/// <remarks>
/// There is no <c>AddCalloraWorkspace</c> counterpart on purpose: the domain
/// services this surface consumes (workspace store, template resolution) live in
/// Core and are registered by <c>AddCalloraHost</c>. This slice contributes only
/// minimal-API routes, so a service-registration hook would be empty ceremony
/// (REV2 §4).
/// </remarks>
public static class CalloraWorkspaceExtensions
{
    /// <summary>Maps the storefront-facing endpoints.</summary>
    public static WebApplication MapCalloraWorkspace(this WebApplication app)
    {
        app.MapWorkspacePublicEndpoints();
        app.MapWorkspaceThemeEndpoints();
        return app;
    }
}
