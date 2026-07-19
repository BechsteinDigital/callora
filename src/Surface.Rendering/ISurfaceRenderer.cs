namespace Callora.Surface.Rendering;

/// <summary>
/// Renders one surface template to HTML inside the hardened Nunjucks-on-Jint
/// sandbox (ADR-015 §7/§8 rev.). The template sees only the allowlisted
/// <see cref="SurfaceRenderContext"/>; failures surface as
/// <see cref="SurfaceTemplateException"/>.
/// </summary>
public interface ISurfaceRenderer
{
    /// <summary>Renders a self-contained template — includes are disabled.</summary>
    string Render(string templateText, SurfaceRenderContext context);

    /// <summary>
    /// Renders with <c>@bundle/path</c> includes enabled, resolved only against the
    /// bundles in <paramref name="bundleChain"/> (the surface's resolved chain).
    /// Paths outside a bundle root are rejected (ADR-015 §8). Requires a configured
    /// <see cref="ISurfaceTemplateBundleProvider"/>; without one, includes stay off.
    /// </summary>
    string Render(string templateText, SurfaceRenderContext context, IReadOnlyList<string> bundleChain);
}
