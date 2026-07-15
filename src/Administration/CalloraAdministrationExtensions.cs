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
    /// <summary>Registers Administration services. Currently none — the domain
    /// services live in Core (AddCalloraHost); this is the forward-compatible
    /// seam for admin-only registrations.</summary>
    public static WebApplicationBuilder AddCalloraAdministration(this WebApplicationBuilder builder) => builder;

    /// <summary>Maps the operator-facing endpoints.</summary>
    public static WebApplication MapCalloraAdministration(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<BackendHostOptions>();

        app.MapRbacEndpoints();
        app.MapUserEndpoints();
        app.MapPluginEndpoints();
        app.MapPluginAdminExtensionEndpoints();
        app.MapPluginAssetEndpoints(options);
        app.MapEntitlementSyncEndpoints();
        app.MapFeatureEndpoints();
        app.MapBusinessEventEndpoints();
        app.MapThemeEndpoints();

        // Tenant management is feature-gated, mirroring the host composition.
        if (options.EnableTenantManagementApi)
        {
            app.MapTenantEndpoints();
        }

        return app;
    }
}
