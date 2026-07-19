using Callora.Surface.Rendering;
using Callora.Surface.Rendering.Rendering;
using Xunit;

namespace Callora.Core.Tests.Surface;

public sealed class ScribanSurfaceRendererTests
{
    private static SurfaceRenderContext Context(IReadOnlyDictionary<string, string>? tokens = null) => new(
        TenantKey: "tenant-a",
        WorkspaceKey: "workspace-a",
        SurfaceKey: "default",
        SurfaceType: "spa",
        Locale: "de",
        Tokens: tokens ?? new Dictionary<string, string>());

    private readonly ScribanSurfaceRenderer _renderer = new();

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
    public void Render_DeniesReflection_HardFails()
    {
        // A template probing for CLR reflection must not reach a .NET type: the
        // member filter denies all CLR members, so the access is rejected outright
        // (fail-closed) rather than leaking a type.
        var ex = Assert.Throws<SurfaceTemplateException>(
            () => _renderer.Render("{{ workspace.key.GetType().FullName }}", Context()));

        Assert.DoesNotContain("System.String", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_LoopBeyondLimit_ThrowsSurfaceTemplateException()
    {
        // LoopLimit (1000) bounds template DoS; a larger loop is rejected.
        var attack = "{{ for i in 1..50000 }}x{{ end }}";

        Assert.Throws<SurfaceTemplateException>(() => _renderer.Render(attack, Context()));
    }

    [Fact]
    public void Render_IncludeWithoutLoader_ThrowsSurfaceTemplateException()
    {
        // No ITemplateLoader is configured → no file access surface at all.
        Assert.Throws<SurfaceTemplateException>(() => _renderer.Render("{{ include 'etc/passwd' }}", Context()));
    }

    [Fact]
    public void Render_ParseError_ThrowsSurfaceTemplateException()
    {
        Assert.Throws<SurfaceTemplateException>(() => _renderer.Render("{{ if }}{{ broken", Context()));
    }
}
