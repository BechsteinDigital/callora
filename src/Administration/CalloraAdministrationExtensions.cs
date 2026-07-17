using Callora.Administration.Api;
using Callora.Core.Application.Policies;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Administration;

/// <summary>
/// Composition entry for the Administration module — the operator-facing API
/// surface (global, SuperAdmin). The distribution skeleton calls these; Core
/// never references Administration. Domain logic (Identity/RBAC, Tenancy, plugin
/// runtime, …) stays in Core, this module owns only the operator API (REV2 §4).
/// </summary>
public static class CalloraAdministrationExtensions
{
    /// <summary>Registers Administration services. The domain services live in
    /// Core (AddCalloraHost); this only adds the module's MVC controllers
    /// (IntegrationsController) as an application part so the host's
    /// MapControllers() discovers them.</summary>
    public static WebApplicationBuilder AddCalloraAdministration(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(CalloraAdministrationExtensions).Assembly);
        return builder;
    }

    /// <summary>Maps the operator-facing minimal-API endpoints.</summary>
    public static WebApplication MapCalloraAdministration(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<BackendHostOptions>();

        app.MapAdminContextEndpoints();
        app.MapRbacEndpoints();
        app.MapUserEndpoints();
        app.MapPluginEndpoints();
        app.MapPluginAdminExtensionEndpoints();
        app.MapPluginAssetEndpoints(options);
        app.MapEntitlementSyncEndpoints();
        app.MapFeatureEndpoints();
        app.MapBusinessEventEndpoints();
        app.MapThemeEndpoints();

        // Operator /api/* surface: workspace administration plus the cross-cutting
        // resource endpoints. All are backend-permission gated (some additionally
        // workspace-scoped as a tenant filter) — the operator backend, not the
        // storefront, so they live here rather than in Callora.Workspace (REV2 §4).
        app.MapWorkspaceEndpoints();
        app.MapCustomFieldEndpoints();
        app.MapFlowEndpoints();
        app.MapMediaEndpoints();
        app.MapNotificationEndpoints();
        app.MapWebhookEndpoints();
        app.MapJobEndpoints();
        app.MapSystemConfigEndpoints();

        // Tenant management is feature-gated, mirroring the host composition.
        if (options.EnableTenantManagementApi)
        {
            app.MapTenantEndpoints();
        }

        // Lowest priority: the admin SPA fallback for client-side routing.
        app.MapAdminSpaFallback();

        return app;
    }

    /// <summary>
    /// Serves the admin SPA: any GET under /admin/* that does not match a static
    /// file or API route falls back to the SPA entry document, so the client-side
    /// router handles deep links (e.g. /admin/users). /admin collides with no
    /// reserved API prefix (all are /api/* or /workspace/*).
    /// </summary>
    public static IEndpointRouteBuilder MapAdminSpaFallback(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapFallbackToFile("/admin/{**path}", "admin/index.html");
        return endpoints;
    }
}
