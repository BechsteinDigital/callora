using Callora.Core.Infrastructure.Plugins;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Plugins;

public sealed class PluginAssetCachingTests
{
    [Fact]
    public void ResolveCacheControl_VersionedRequest_IsImmutable()
    {
        // A ?v=<hash> URL changes on every content change, so it may be cached hard.
        Assert.Equal(PluginAssetCaching.ImmutableCacheControl, PluginAssetCaching.ResolveCacheControl(versioned: true));
        Assert.Contains("immutable", PluginAssetCaching.ResolveCacheControl(versioned: true), StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCacheControl_UnversionedRequest_MustRevalidate()
    {
        // A hash-less URL is fixed, so it must revalidate or an upgrade could serve stale.
        Assert.Equal(PluginAssetCaching.RevalidateCacheControl, PluginAssetCaching.ResolveCacheControl(versioned: false));
        Assert.Equal("no-cache", PluginAssetCaching.ResolveCacheControl(versioned: false));
    }

    [Fact]
    public void Apply_VersionedPluginAsset_SetsImmutable()
    {
        var ctx = BuildContext("/plugin-assets/voip/app/surface/main.js", "?v=abc123");

        PluginAssetCaching.Apply(ctx);

        Assert.Equal(PluginAssetCaching.ImmutableCacheControl, ctx.Context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public void Apply_UnversionedPluginAsset_SetsNoCache()
    {
        var ctx = BuildContext("/plugin-assets/voip/app/surface/main.js", query: null);

        PluginAssetCaching.Apply(ctx);

        Assert.Equal(PluginAssetCaching.RevalidateCacheControl, ctx.Context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public void Apply_OutsidePluginAssets_LeavesHeaderUntouched()
    {
        // Even a versioned request to another static root must keep the framework default.
        var ctx = BuildContext("/surface-app/surface.js", "?v=abc123");

        PluginAssetCaching.Apply(ctx);

        Assert.True(string.IsNullOrEmpty(ctx.Context.Response.Headers.CacheControl.ToString()));
    }

    private static StaticFileResponseContext BuildContext(string path, string? query)
    {
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        if (query is not null)
        {
            http.Request.QueryString = new QueryString(query);
        }

        return new StaticFileResponseContext(http, new NotFoundFileInfo(path));
    }
}
