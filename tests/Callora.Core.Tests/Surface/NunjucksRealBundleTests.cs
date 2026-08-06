using Callora.Surface.Rendering;
using Callora.Surface.Rendering.Rendering;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// End-to-end: a real multi-file .njk bundle in a plugin views root (mirroring the
/// TemplateAlpha layout — relative extends/include, not @bundle namespaces) renders
/// with native Nunjucks inheritance through the confined loader.
/// </summary>
[Collection(SurfaceRenderingCollection.Name)]
public sealed class NunjucksRealBundleTests : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("callora-bundle-");
    private readonly string _viewsRoot;

    public NunjucksRealBundleTests()
    {
        // <temp>/views/surface/{base.njk, partials/sidebar.njk, layouts/dashboard.njk}
        _viewsRoot = Path.Combine(_temp.FullName, "views", "surface");
        Directory.CreateDirectory(Path.Combine(_viewsRoot, "partials"));
        Directory.CreateDirectory(Path.Combine(_viewsRoot, "layouts"));

        File.WriteAllText(Path.Combine(_viewsRoot, "base.njk"),
            "<html><head><title>{% block title %}Workspace{% endblock %}</title></head>" +
            "<body><aside>{% include \"partials/sidebar.njk\" %}</aside>" +
            "<main class=\"shell\">{% block content %}{% endblock %}</main></body></html>");

        File.WriteAllText(Path.Combine(_viewsRoot, "partials", "sidebar.njk"),
            "<nav data-workspace=\"{{ workspace.key }}\">SIDEBAR</nav>");

        File.WriteAllText(Path.Combine(_viewsRoot, "layouts", "dashboard.njk"),
            "{% extends \"base.njk\" %}" +
            "{% block title %}Dashboard {{ super() }}{% endblock %}" +
            "{% block content %}<h1>{{ surface.key }}</h1>{% endblock %}");
    }

    public void Dispose() => _temp.Delete(recursive: true);

    private static SurfaceRenderContext Context() => new(
        "tenant-a", "workspace-a", "default", "spa", "de", new Dictionary<string, string>());

    [Fact]
    public void Render_RealBundle_ComposesExtendIncludeAndBlocks()
    {
        var provider = new DirectorySurfaceTemplateBundleProvider(
            new Dictionary<string, string> { ["alpha"] = _viewsRoot });
        var renderer = new NunjucksSurfaceRenderer(provider);

        // The entry template (dashboard.njk) extends base.njk and pulls in a partial —
        // all by relative name, resolved against the plugin's views root.
        var entry = File.ReadAllText(Path.Combine(_viewsRoot, "layouts", "dashboard.njk"));
        var html = renderer.Render(entry, Context(), ["alpha"]);

        // block override + super()
        Assert.Contains("<title>Dashboard Workspace</title>", html, StringComparison.Ordinal);
        // relative include resolved and rendered with context
        Assert.Contains("SIDEBAR", html, StringComparison.Ordinal);
        Assert.Contains("data-workspace=\"workspace-a\"", html, StringComparison.Ordinal);
        // base skeleton + content block
        Assert.Contains("class=\"shell\"", html, StringComparison.Ordinal);
        Assert.Contains("<h1>default</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_RelativeName_StaysConfined_NoTraversal()
    {
        File.WriteAllText(Path.Combine(_temp.FullName, "secret.txt"), "TOPSECRET");
        var provider = new DirectorySurfaceTemplateBundleProvider(
            new Dictionary<string, string> { ["alpha"] = _viewsRoot });
        var renderer = new NunjucksSurfaceRenderer(provider);

        // A relative escape must be rejected just like an @bundle one.
        var ex = Assert.Throws<SurfaceTemplateException>(
            () => renderer.Render("{% include \"../../secret.txt\" %}", Context(), ["alpha"]));

        Assert.DoesNotContain("TOPSECRET", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_RelativeName_AbsolutePath_IsRejected()
    {
        // The subtlest vector of the relative branch: a rooted name replaces the
        // bundle root under Path.Combine instead of nesting under it — only
        // IsUnderRoot stops it. Prove it holds directly.
        var secret = Path.Combine(_temp.FullName, "secret.txt");
        File.WriteAllText(secret, "TOPSECRET");
        var provider = new DirectorySurfaceTemplateBundleProvider(
            new Dictionary<string, string> { ["alpha"] = _viewsRoot });
        var renderer = new NunjucksSurfaceRenderer(provider);

        var ex = Assert.Throws<SurfaceTemplateException>(
            () => renderer.Render($"{{% include \"{secret.Replace("\\", "\\\\", StringComparison.Ordinal)}\" %}}", Context(), ["alpha"]));

        Assert.DoesNotContain("TOPSECRET", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_RelativeName_PrefixCollisionSiblingRoot_IsRejected()
    {
        // Sibling directory whose path is a string-prefix of the bundle root
        // (…/workspace vs …/workspace-evil) must not resolve on the relative path
        // either — same confinement as the @bundle branch.
        var evil = _viewsRoot + "-evil";
        Directory.CreateDirectory(evil);
        File.WriteAllText(Path.Combine(evil, "x.njk"), "EVIL");
        var basename = Path.GetFileName(_viewsRoot);
        var provider = new DirectorySurfaceTemplateBundleProvider(
            new Dictionary<string, string> { ["alpha"] = _viewsRoot });
        var renderer = new NunjucksSurfaceRenderer(provider);

        var ex = Assert.Throws<SurfaceTemplateException>(
            () => renderer.Render($"{{% include \"../{basename}-evil/x.njk\" %}}", Context(), ["alpha"]));

        Assert.DoesNotContain("EVIL", ex.Message, StringComparison.Ordinal);
    }
}
