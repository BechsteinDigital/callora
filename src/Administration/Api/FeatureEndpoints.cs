using Callora.Core.Application.Features;

namespace Callora.Administration.Api;

/// <summary>
/// Read-only feature-flag query API (PLAT-263). Flags are non-sensitive
/// toggles that any authenticated caller (including the shells) may read to
/// adapt behaviour; they are configured centrally, not per request.
/// </summary>
public static class FeatureEndpoints
{
    public static IEndpointRouteBuilder MapFeatureEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/features")
            .WithTags("Features")
            .RequireAuthorization();

        group.MapGet("/", (IFeatureFlagService features) =>
                Results.Ok(features.GetAll()
                    .Select(flag => new FeatureFlagApiResponse(flag.Key, flag.Value))
                    .ToArray()))
            .WithName("Features_List")
            .Produces<FeatureFlagApiResponse[]>();

        group.MapGet("/{key}", (string key, IFeatureFlagService features) =>
                Results.Ok(new FeatureFlagApiResponse(key, features.IsEnabled(key))))
            .WithName("Features_Get")
            .Produces<FeatureFlagApiResponse>();

        return app;
    }
}
