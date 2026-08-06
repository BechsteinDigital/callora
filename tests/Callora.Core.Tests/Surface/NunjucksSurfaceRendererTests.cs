using Callora.Surface.Rendering;
using Callora.Surface.Rendering.Rendering;
using Xunit;

namespace Callora.Core.Tests.Surface;

[Collection(SurfaceRenderingCollection.Name)]
public sealed class NunjucksSurfaceRendererTests
{
    private static SurfaceRenderContext Context(IReadOnlyDictionary<string, string>? tokens = null) => new(
        "tenant-a", "workspace-a", "default", "spa", "de", tokens ?? new Dictionary<string, string>());

    private readonly NunjucksSurfaceRenderer _renderer = new();

    [Fact]
    public void Render_SpaRootShell_EmitsTheWorkspaceAndSurfaceContext()
    {
        var html = _renderer.Render(SurfaceShellTemplates.SpaRoot, Context());

        Assert.Contains("id=\"callora-app\"", html, StringComparison.Ordinal);
        Assert.Contains("data-workspace=\"workspace-a\"", html, StringComparison.Ordinal);
        Assert.Contains("data-surface=\"default\"", html, StringComparison.Ordinal);
        Assert.Contains("lang=\"de\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ExposesTokens()
    {
        var html = _renderer.Render(
            "{{ tokens.primary }}",
            Context(new Dictionary<string, string> { ["primary"] = "#ff0000" }));

        Assert.Equal("#ff0000", html);
    }

    [Fact]
    public void Render_AutoescapesVariableOutput()
    {
        // A token value with HTML must be escaped in the output (XSS defence).
        var html = _renderer.Render(
            "{{ tokens.evil }}",
            Context(new Dictionary<string, string> { ["evil"] = "<script>x</script>" }));

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_NativeControlFlow_Works()
    {
        var html = _renderer.Render(
            "{% if surface.type == 'spa' %}SPA{% else %}OTHER{% endif %}",
            Context());

        Assert.Equal("SPA", html);
    }

    [Fact]
    public void Render_DeniesClrAccess_NoDotNetReach()
    {
        // Jint has no CLR access configured — there is no host bridge to reach a
        // .NET type from the template.
        var ex = Assert.Throws<SurfaceTemplateException>(
            () => _renderer.Render("{{ System.IO.File.ReadAllText('/etc/hostname') }}", Context()));

        Assert.DoesNotContain("root", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_InfiniteLoopStyleDoS_IsBoundedBySandbox()
    {
        // A runaway loop must not hang: the statement/timeout limits abort it.
        var attack = "{% for i in range(0, 100000000) %}{{ i }}{% endfor %}";

        Assert.Throws<SurfaceTemplateException>(() => _renderer.Render(attack, Context()));
    }

    [Fact]
    public void Render_ParseError_ThrowsSurfaceTemplateException()
    {
        Assert.Throws<SurfaceTemplateException>(() => _renderer.Render("{% if %}{% endif", Context()));
    }
}
