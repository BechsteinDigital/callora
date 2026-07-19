using Callora.Surface.Rendering;
using Callora.Surface.Rendering.Rendering;
using Xunit;

namespace Callora.Core.Tests.Surface;

public sealed class BundleTemplateLoaderTests : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("callora-surface-");
    private readonly string _bundleRoot;

    public BundleTemplateLoaderTests()
    {
        _bundleRoot = Path.Combine(_temp.FullName, "bundle");
        Directory.CreateDirectory(_bundleRoot);
        File.WriteAllText(Path.Combine(_bundleRoot, "partial.html"), "HELLO {{ workspace.key }}");
        // A file OUTSIDE the bundle root the loader must never reach via traversal.
        File.WriteAllText(Path.Combine(_temp.FullName, "secret.txt"), "TOPSECRET");
    }

    public void Dispose() => _temp.Delete(recursive: true);

    private static SurfaceRenderContext Context() => new(
        "tenant-a", "workspace-a", "default", "spa", "de", new Dictionary<string, string>());

    private sealed class DictBundleProvider(IReadOnlyDictionary<string, string> roots) : ISurfaceTemplateBundleProvider
    {
        public bool TryGetBundleRoot(string bundleId, out string? rootFullPath)
        {
            var found = roots.TryGetValue(bundleId, out var value);
            rootFullPath = value;
            return found;
        }
    }

    private ScribanSurfaceRenderer RendererWith(params (string bundleId, string root)[] bundles) =>
        new(new DictBundleProvider(bundles.ToDictionary(x => x.bundleId, x => x.root, StringComparer.OrdinalIgnoreCase)));

    [Fact]
    public void Render_IncludeFromInScopeBundle_LoadsAndRendersTheFile()
    {
        var renderer = RendererWith(("acme", _bundleRoot));

        var html = renderer.Render("{{ include '@acme/partial.html' }}", Context(), ["acme"]);

        Assert.Equal("HELLO workspace-a", html);
    }

    [Fact]
    public void Render_TraversalOutsideBundle_IsRejected_AndCannotReadSecret()
    {
        var renderer = RendererWith(("acme", _bundleRoot));

        var ex = Assert.Throws<SurfaceTemplateException>(
            () => renderer.Render("{{ include '@acme/../secret.txt' }}", Context(), ["acme"]));

        Assert.DoesNotContain("TOPSECRET", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_NestedSubfolder_LoadsLegitimately()
    {
        var layouts = Path.Combine(_bundleRoot, "layouts");
        Directory.CreateDirectory(layouts);
        File.WriteAllText(Path.Combine(layouts, "base.html"), "BASE {{ surface.key }}");
        var renderer = RendererWith(("acme", _bundleRoot));

        var html = renderer.Render("{{ include '@acme/layouts/base.html' }}", Context(), ["acme"]);

        Assert.Equal("BASE default", html);
    }

    [Fact]
    public void Render_PrefixCollisionSiblingRoot_IsRejected()
    {
        // A sibling directory whose name shares the bundle-root prefix must NOT be
        // reachable — the containment check appends a separator to reject this.
        var evil = _bundleRoot + "-evil";
        Directory.CreateDirectory(evil);
        File.WriteAllText(Path.Combine(evil, "x.html"), "EVIL");
        var renderer = RendererWith(("acme", _bundleRoot));

        var ex = Assert.Throws<SurfaceTemplateException>(
            () => renderer.Render("{{ include '@acme/../bundle-evil/x.html' }}", Context(), ["acme"]));

        Assert.DoesNotContain("EVIL", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_AbsolutePathEscape_IsRejected()
    {
        var renderer = RendererWith(("acme", _bundleRoot));
        var absoluteSecret = Path.Combine(_temp.FullName, "secret.txt");

        Assert.Throws<SurfaceTemplateException>(
            () => renderer.Render($"{{{{ include '@acme/{absoluteSecret}' }}}}", Context(), ["acme"]));
    }

    [Fact]
    public void Render_BundleNotInScope_IsRejected()
    {
        // The provider knows "other", but the surface chain only allows "acme".
        var renderer = RendererWith(("acme", _bundleRoot), ("other", _bundleRoot));

        Assert.Throws<SurfaceTemplateException>(
            () => renderer.Render("{{ include '@other/partial.html' }}", Context(), ["acme"]));
    }

    [Fact]
    public void Render_UnknownBundle_IsRejected()
    {
        var renderer = RendererWith(("acme", _bundleRoot));

        Assert.Throws<SurfaceTemplateException>(
            () => renderer.Render("{{ include '@ghost/partial.html' }}", Context(), ["ghost"]));
    }

    [Fact]
    public void Render_MalformedIncludeName_IsRejected()
    {
        var renderer = RendererWith(("acme", _bundleRoot));

        Assert.Throws<SurfaceTemplateException>(
            () => renderer.Render("{{ include 'partial.html' }}", Context(), ["acme"]));
    }

    [Fact]
    public void Render_TwoArgOverload_KeepsIncludesDisabled()
    {
        var renderer = RendererWith(("acme", _bundleRoot));

        // The 2-arg overload never installs a loader — includes fail even for an
        // in-scope bundle.
        Assert.Throws<SurfaceTemplateException>(
            () => renderer.Render("{{ include '@acme/partial.html' }}", Context()));
    }

    [Fact]
    public void Render_WithoutProvider_KeepsIncludesDisabledEvenWithChain()
    {
        var renderer = new ScribanSurfaceRenderer(); // no bundle provider configured

        Assert.Throws<SurfaceTemplateException>(
            () => renderer.Render("{{ include '@acme/partial.html' }}", Context(), ["acme"]));
    }
}
