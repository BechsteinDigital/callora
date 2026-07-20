using Microsoft.AspNetCore.StaticFiles;

namespace Callora.Core.Infrastructure.Plugins;

/// <summary>
/// The cache policy for published plugin assets under <c>/plugin-assets</c>. A request
/// that carries a version query (<c>?v=&lt;contentHash&gt;</c>) targets an immutable URL —
/// a content change yields a new hash, hence a new URL — so it may be cached hard and
/// never revalidated. An unversioned request (a hash-less legacy manifest, or a bundle
/// sub-resource referenced without a version) must revalidate against its ETag so an
/// upgraded file is never served stale.
/// </summary>
internal static class PluginAssetCaching
{
    internal const string ImmutableCacheControl = "public, max-age=31536000, immutable";
    internal const string RevalidateCacheControl = "no-cache";

    internal static string ResolveCacheControl(bool versioned) =>
        versioned ? ImmutableCacheControl : RevalidateCacheControl;

    /// <summary>
    /// The <see cref="StaticFileOptions.OnPrepareResponse"/> hook: applies the cache
    /// policy to responses under <c>/plugin-assets</c> only, leaving every other static
    /// root at the framework default. Versioned (<c>?v</c>) requests become immutable,
    /// everything else must revalidate.
    /// </summary>
    internal static void Apply(StaticFileResponseContext context)
    {
        var request = context.Context.Request;
        if (!request.Path.StartsWithSegments("/plugin-assets"))
        {
            return;
        }

        context.Context.Response.Headers.CacheControl =
            ResolveCacheControl(request.Query.ContainsKey("v"));
    }
}
