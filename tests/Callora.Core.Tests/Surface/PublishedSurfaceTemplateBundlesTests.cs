using Callora.Surface.Rendering;
using Callora.Surface.Rendering.Rendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// End-to-end for the published-bundle wiring (engine baustein E2c): a plugin whose
/// Nunjucks views were laid down by the UI asset publisher at
/// <c>&lt;webRoot&gt;/plugin-assets/&lt;id&gt;/views/surface</c> resolves its surface
/// entry and renders through the confined loader — the same path the /surface/render
/// endpoint drives.
/// </summary>
public sealed class PublishedSurfaceTemplateBundlesTests : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("callora-published-");
    private readonly string _viewsRoot;
    private readonly PublishedSurfaceTemplateBundles _bundles;

    public PublishedSurfaceTemplateBundlesTests()
    {
        // Mirror the publisher's output tree: <webRoot>/plugin-assets/alpha/views/surface.
        _viewsRoot = Path.Combine(_temp.FullName, "plugin-assets", "alpha", "views", "surface");
        Directory.CreateDirectory(Path.Combine(_viewsRoot, "partials"));

        File.WriteAllText(Path.Combine(_viewsRoot, "base.njk"),
            "<html><head><title>{% block title %}Workspace{% endblock %}</title></head>" +
            "<body><aside>{% include \"partials/sidebar.njk\" %}</aside>" +
            "<main class=\"shell\">{% block content %}{% endblock %}</main></body></html>");
        File.WriteAllText(Path.Combine(_viewsRoot, "partials", "sidebar.njk"),
            "<nav data-workspace=\"{{ workspace.key }}\">SIDEBAR</nav>");
        File.WriteAllText(Path.Combine(_viewsRoot, "index.njk"),
            "{% extends \"base.njk\" %}" +
            "{% block title %}Portal {{ super() }}{% endblock %}" +
            "{% block content %}<h1>{{ workspace.key }}</h1>{% endblock %}");

        _bundles = new PublishedSurfaceTemplateBundles(new FakeWebHostEnvironment(_temp.FullName));
    }

    public void Dispose() => _temp.Delete(recursive: true);

    private static SurfaceRenderContext Context() => new(
        "tenant-a", "workspace-a", "default", "spa", "de", new Dictionary<string, string>());

    [Fact]
    public void PublishedPlugin_ResolvesEntry_And_RendersThroughConfinedLoader()
    {
        Assert.True(_bundles.TryGetBundleRoot("alpha", out var root));
        Assert.Equal(_viewsRoot, root);

        var entry = _bundles.TryReadEntryTemplate("alpha");
        Assert.NotNull(entry);

        var renderer = new NunjucksSurfaceRenderer(_bundles);
        var html = renderer.Render(entry!, Context(), ["alpha"]);

        Assert.Contains("<title>Portal Workspace</title>", html, StringComparison.Ordinal);
        Assert.Contains("data-workspace=\"workspace-a\"", html, StringComparison.Ordinal);
        Assert.Contains("SIDEBAR", html, StringComparison.Ordinal);
        Assert.Contains("class=\"shell\"", html, StringComparison.Ordinal);
        Assert.Contains("<h1>workspace-a</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void TryReadEntryTemplate_PrefersIndexOverMain()
    {
        File.WriteAllText(Path.Combine(_viewsRoot, "main.njk"), "MAIN-ENTRY");

        var entry = _bundles.TryReadEntryTemplate("alpha");

        Assert.NotNull(entry);
        Assert.DoesNotContain("MAIN-ENTRY", entry!, StringComparison.Ordinal);
        Assert.Contains("{% extends", entry!, StringComparison.Ordinal);
    }

    [Fact]
    public void TryReadEntryTemplate_FallsBackToMainWhenNoIndex()
    {
        File.Delete(Path.Combine(_viewsRoot, "index.njk"));
        File.WriteAllText(Path.Combine(_viewsRoot, "main.njk"), "MAIN-ENTRY");

        Assert.Equal("MAIN-ENTRY", _bundles.TryReadEntryTemplate("alpha"));
    }

    [Fact]
    public void PluginWithoutEntry_ReturnsNull_ForSpaFallback()
    {
        // A published views root that has partials but no index/main entry.
        var noEntry = Path.Combine(_temp.FullName, "plugin-assets", "beta", "views", "surface");
        Directory.CreateDirectory(noEntry);
        File.WriteAllText(Path.Combine(noEntry, "base.njk"), "<html></html>");

        Assert.True(_bundles.TryGetBundleRoot("beta", out _));
        Assert.Null(_bundles.TryReadEntryTemplate("beta"));
    }

    [Fact]
    public void UnknownPlugin_TryGetBundleRoot_False_And_NoEntry()
    {
        Assert.False(_bundles.TryGetBundleRoot("ghost", out var root));
        Assert.Null(root);
        Assert.Null(_bundles.TryReadEntryTemplate("ghost"));
    }

    [Theory]
    [InlineData("../../secret")]
    [InlineData("../beta")]
    [InlineData("")]
    [InlineData("   ")]
    public void CraftedBundleId_IsRejected(string bundleId)
    {
        // A leaked secret next to the plugin-assets root that a traversal id would target.
        File.WriteAllText(Path.Combine(_temp.FullName, "secret"), "TOPSECRET");

        Assert.False(_bundles.TryGetBundleRoot(bundleId, out var root));
        Assert.Null(root);
        Assert.Null(_bundles.TryReadEntryTemplate(bundleId));
    }

    [Fact]
    public void AbsolutePathBundleId_IsRejected()
    {
        // A rooted id replaces the assets root under Path.Combine, so only the
        // containment check stops it — prove it directly with an absolute path.
        var secret = Path.Combine(_temp.FullName, "secret.txt");
        File.WriteAllText(secret, "TOPSECRET");

        Assert.False(_bundles.TryGetBundleRoot(secret, out var root));
        Assert.Null(root);
        Assert.Null(_bundles.TryReadEntryTemplate(secret));
    }

    private sealed class FakeWebHostEnvironment(string webRootPath) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = webRootPath;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Callora.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
    }
}
