using Callora.Core.Application.Policies;

namespace Callora.Administration.Api;

public static class PluginAssetEndpoints
{
    public static void MapPluginAssetEndpoints(this IEndpointRouteBuilder endpoints, BackendHostOptions hostOptions)
    {
        var manifestRoute = string.IsNullOrWhiteSpace(hostOptions.PluginManifestUrl)
            ? "/manifests/plugin-ui-assets.manifest.json"
            : hostOptions.PluginManifestUrl.Trim();
        if (!manifestRoute.StartsWith('/'))
        {
            manifestRoute = "/" + manifestRoute;
        }

        endpoints.MapGet(manifestRoute, (HttpContext httpContext, IWebHostEnvironment environment) =>
            {
                var webRoot = string.IsNullOrWhiteSpace(environment.WebRootPath)
                    ? Path.Combine(AppContext.BaseDirectory, "wwwroot")
                    : environment.WebRootPath;
                var manifestPath = Path.Combine(webRoot, "plugin-assets", ".build", "ui-assets.manifest.json");
                if (!File.Exists(manifestPath))
                {
                    return Results.NotFound();
                }

                // The manifest is the version index that cache-busts the assets it points
                // to; it must itself always be revalidated, else a stale manifest would
                // keep pinning old ?v= hashes and defeat the busting.
                httpContext.Response.Headers.CacheControl = "no-cache";
                return Results.File(manifestPath, "application/json; charset=utf-8");
            })
            .AllowAnonymous()
            .ExcludeFromDescription();
    }
}
