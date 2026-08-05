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
        app.MapPluginWebSocketEndpoints();
        app.MapPluginPublicHttpEndpoints();
        app.MapPluginAssetEndpoints(options);
        app.MapEntitlementSyncEndpoints();
        app.MapEntitlementManagementEndpoints();
        app.MapFeatureEndpoints();
        app.MapBusinessEventEndpoints();
        app.MapThemeEndpoints();
        app.MapSurfaceThemeEndpoints();
        app.MapSurfaceIdentityEndpoints();
        app.MapContractCatalogEndpoints();
        app.MapPluginSurfaceApiEndpoints(
            Callora.Core.Infrastructure.Security.BackendRateLimiting.ApiPolicy);

        // Operator /api/* surface: workspace administration plus the cross-cutting
        // resource endpoints. All are backend-permission gated (some additionally
        // workspace-scoped as a tenant filter) — the operator backend, not the
        // storefront, so they live here rather than in Callora.Workspace (REV2 §4).
        app.MapWorkspaceEndpoints();
        app.MapSurfaceEndpoints();
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
    /// Serves the admin SPA under /admin/*. This is a concrete route, NOT
    /// MapFallbackToFile: a fallback has lowest priority and would lose to the
    /// workspace storefront catch-all <c>/{**path}</c>, which then redirects
    /// /admin to <see cref="BackendHostOptions.AdminShellBaseUrl"/> — a
    /// self-redirect loop when the shell is served locally ("/admin/"). As a
    /// concrete route it outranks that catch-all. Static assets under /admin are
    /// served earlier by UseStaticFiles; every other /admin request returns the
    /// SPA entry document so the client-side router handles deep links
    /// (e.g. /admin/users). The <c>nonfile</c> constraint lets asset paths
    /// (/admin/assets/*.js|css, which carry an extension) fall through to the
    /// static-file/SWA pipeline instead of being answered with index.html.
    /// index.html is resolved through the web-root file provider, which includes
    /// static web assets under <c>dotnet run</c> (dev) and the physical file
    /// after publish.
    /// </summary>
    public static IEndpointRouteBuilder MapAdminSpaFallback(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet("/admin/{**path:nonfile}", (IWebHostEnvironment environment) =>
        {
            var indexFile = environment.WebRootFileProvider.GetFileInfo("admin/index.html");
            return indexFile.Exists
                ? Results.File(indexFile.CreateReadStream(), "text/html; charset=utf-8")
                : Results.NotFound();
        })
        .AllowAnonymous()
        .ExcludeFromDescription();
        return endpoints;
    }
}
