using Callora.Core.Application.Surfaces;
using Callora.Surface.Rendering.Api;
using Callora.Surface.Rendering.Api.SurfaceContext;
using Callora.Surface.Rendering.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Surface.Rendering;

/// <summary>
/// Composition entry for the surface rendering layer (ADR-015 Schicht 2). The
/// distribution skeleton composes this only when it wants server-side rendered
/// surfaces; a purely headless deployment omits it and pulls in neither the
/// engine nor the shell.
/// </summary>
public static class CalloraSurfaceRenderingExtensions
{
    /// <summary>
    /// Registers the hardened Nunjucks-on-Jint renderer and the published-plugin bundle
    /// source. Binding <see cref="ISurfaceTemplateBundleProvider"/> here enables the
    /// renderer's include/extends resolution against installed template plugins' views.
    /// </summary>
    public static IServiceCollection AddCalloraSurfaceRendering(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<PublishedSurfaceTemplateBundles>();
        services.AddSingleton<ISurfaceTemplateBundleProvider>(
            sp => sp.GetRequiredService<PublishedSurfaceTemplateBundles>());
        services.AddSingleton<ISurfaceRenderer, NunjucksSurfaceRenderer>();

        // This module's controllers live in ITS assembly, and AddControllers() only scans the
        // entry assembly. Without this part the context bridge route simply would not exist —
        // no error, no log line, just a 404 nobody can explain.
        services.AddControllers().AddApplicationPart(typeof(SurfaceContextController).Assembly);

        // One broadcaster per process: a subscription belongs to the process that accepted
        // its socket, and a value is published to the connections that process holds.
        services.AddSingleton<SurfaceContextBroadcaster>();
        services.AddSingleton<ISurfaceContextBroadcaster>(
            sp => sp.GetRequiredService<SurfaceContextBroadcaster>());
        return services;
    }

    /// <summary>Maps the public server-rendered surface route.</summary>
    public static WebApplication MapCalloraSurfaceRendering(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapSurfaceHandoffEndpoints();
        app.MapSurfaceRenderEndpoints();
        return app;
    }
}
