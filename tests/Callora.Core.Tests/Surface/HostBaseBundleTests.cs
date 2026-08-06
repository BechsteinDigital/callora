using Callora.Surface.Rendering;
using Callora.Surface.Rendering.Rendering;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// The host ships its own template bundle under the reserved id <c>callora</c>. It is
/// addressed like any other — <c>@callora/base.njk</c> — but resolves from the renderer
/// assembly rather than from published plugin assets, and needs no chain entry: a plugin
/// that extends it does not have to declare a dependency on the host it already runs in.
/// </summary>
[Collection(SurfaceRenderingCollection.Name)]
public sealed class HostBaseBundleTests
{
    private static SurfaceRenderContext Context(
        IReadOnlyDictionary<string, string>? tokens = null) => new(
        "tenant-a",
        "workspace-a",
        "default",
        "spa",
        "de",
        tokens ?? new Dictionary<string, string>());

    // No provider at all: the host bundle must resolve without one, because a minimal
    // host composes the renderer before any plugin assets exist.
    private static NunjucksSurfaceRenderer Renderer() => new();

    [Fact]
    public void Extends_HostBase_WithoutAnyChainEntry()
    {
        var html = Renderer().Render(
            "{% extends '@callora/base.njk' %}{% block base_content %}Hallo{% endblock %}",
            Context(),
            []);

        Assert.Contains("Hallo", html, StringComparison.Ordinal);
        Assert.StartsWith("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HostBase_IsNotShadowedByAPluginOfTheSameId()
    {
        // A plugin calling itself "callora" must not be able to replace the base
        // template every other plugin extends.
        using var temp = new TempBundle("base.njk", "IMPOSTOR");
        var renderer = new NunjucksSurfaceRenderer(
            new DirectorySurfaceTemplateBundleProvider(
                new Dictionary<string, string> { ["callora"] = temp.Root }));

        var html = renderer.Render("{% include '@callora/base.njk' %}", Context(), ["callora"]);

        Assert.DoesNotContain("IMPOSTOR", html, StringComparison.Ordinal);
    }

    [Fact]
    public void HostBase_RejectsPathEscape()
    {
        var renderer = Renderer();

        var ex = Assert.Throws<SurfaceTemplateException>(
            () => renderer.Render("{% include '@callora/../../secret.txt' %}", Context(), []));

        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HostBase_EmitsThemeTokensAsCustomProperties()
    {
        var html = Renderer().Render(
            "{% extends '@callora/base.njk' %}",
            Context(new Dictionary<string, string> { ["color.brand"] = "#1f6fe5" }),
            []);

        Assert.Contains("--cal-color-brand: #1f6fe5", html, StringComparison.Ordinal);
    }

    [Fact]
    public void HostBase_TokenValueCannotBreakOutOfTheStyleBlock()
    {
        var html = Renderer().Render(
            "{% extends '@callora/base.njk' %}",
            Context(new Dictionary<string, string> { ["evil"] = "</style><script>alert(1)</script>" }),
            []);

        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
    }

    private sealed class TempBundle : IDisposable
    {
        private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("callora-host-");

        public TempBundle(string fileName, string content)
        {
            Root = Path.Combine(_temp.FullName, "bundle");
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, fileName), content);
        }

        public string Root { get; }

        public void Dispose() => _temp.Delete(recursive: true);
    }
}
