namespace Callora.Surface.Rendering;

/// <summary>
/// Renders one surface template to HTML inside the hardened Scriban sandbox
/// (ADR-015 §7/§8). The template sees only the allowlisted
/// <see cref="SurfaceRenderContext"/>; failures surface as
/// <see cref="SurfaceTemplateException"/>.
/// </summary>
public interface ISurfaceRenderer
{
    string Render(string templateText, SurfaceRenderContext context);
}
