using Callora.Surface.Rendering;
using Callora.Surface.Rendering.Rendering;
using Xunit;

namespace Callora.Core.Tests.Surface;

public sealed class NunjucksBundleLoaderTests : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("callora-njk-");
    private readonly string _bundleRoot;

    public NunjucksBundleLoaderTests()
    {
        _bundleRoot = Path.Combine(_temp.FullName, "bundle");
        Directory.CreateDirectory(_bundleRoot);
        File.WriteAllText(
            Path.Combine(_bundleRoot, "base.njk"),
            "<h1>{% block title %}Base{% endblock %}</h1><main>{% block content %}{% endblock %}</main>");
        File.WriteAllText(Path.Combine(_bundleRoot, "partial.njk"), "P:{{ surface.key }}");
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

    private NunjucksSurfaceRenderer RendererWith(params (string bundleId, string root)[] bundles) =>
        new(new DictBundleProvider(bundles.ToDictionary(x => x.bundleId, x => x.root, StringComparer.OrdinalIgnoreCase)));

    [Fact]
    public void Render_NativeBlockInheritance_WithParentSuper()
    {
        var renderer = RendererWith(("theme", _bundleRoot));
        var child =
            "{% extends '@theme/base.njk' %}" +
            "{% block content %}Hello {{ workspace.key }}{% endblock %}" +
            "{% block title %}{{ super() }} X{% endblock %}";

        var html = renderer.Render(child, Context(), ["theme"]);

        Assert.Equal("<h1>Base X</h1><main>Hello workspace-a</main>", html);
    }

    [Fact]
    public void Render_Include_LoadsFromInScopeBundle()
    {
        var renderer = RendererWith(("theme", _bundleRoot));

        var html = renderer.Render("{% include '@theme/partial.njk' %}", Context(), ["theme"]);

        Assert.Equal("P:default", html);
    }

    [Fact]
    public void Render_TraversalOutsideBundle_IsRejected_AndCannotReadSecret()
    {
        var renderer = RendererWith(("theme", _bundleRoot));

        var ex = Assert.Throws<SurfaceTemplateException>(
            () => renderer.Render("{% include '@theme/../secret.txt' %}", Context(), ["theme"]));

        Assert.DoesNotContain("TOPSECRET", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_PrefixCollisionSiblingRoot_IsRejected()
    {
        var evil = _bundleRoot + "-evil";
        Directory.CreateDirectory(evil);
        File.WriteAllText(Path.Combine(evil, "x.njk"), "EVIL");
        var renderer = RendererWith(("theme", _bundleRoot));

        var ex = Assert.Throws<SurfaceTemplateException>(
            () => renderer.Render("{% include '@theme/../bundle-evil/x.njk' %}", Context(), ["theme"]));

        Assert.DoesNotContain("EVIL", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_BundleNotInScope_IsRejected()
    {
        var renderer = RendererWith(("theme", _bundleRoot), ("other", _bundleRoot));

        Assert.Throws<SurfaceTemplateException>(
            () => renderer.Render("{% include '@other/partial.njk' %}", Context(), ["theme"]));
    }

    [Fact]
    public void Render_UnknownBundle_IsRejected()
    {
        var renderer = RendererWith(("theme", _bundleRoot));

        Assert.Throws<SurfaceTemplateException>(
            () => renderer.Render("{% include '@ghost/partial.njk' %}", Context(), ["ghost"]));
    }

    [Fact]
    public void Render_TwoArgOverload_KeepsIncludesDisabled()
    {
        var renderer = RendererWith(("theme", _bundleRoot));

        Assert.Throws<SurfaceTemplateException>(
            () => renderer.Render("{% include '@theme/partial.njk' %}", Context()));
    }

    [Fact]
    public void Render_WithoutProvider_KeepsIncludesDisabledEvenWithChain()
    {
        var renderer = new NunjucksSurfaceRenderer();

        Assert.Throws<SurfaceTemplateException>(
            () => renderer.Render("{% include '@theme/partial.njk' %}", Context(), ["theme"]));
    }
}
