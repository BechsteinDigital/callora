namespace Callora.Surface.Rendering;

/// <summary>
/// The built-in SurfaceShell (ADR-014 §8.1/§11.2): what the host renders for a surface
/// whose plugins publish no SSR entry of their own.
/// <para>
/// It is a one-line template that extends the host bundle, so a surface with no plugins
/// gets the same header, navigation, footer and theme tokens as one that was designed —
/// rather than the bare app root this used to be. Both contribution paths stay open:
/// server-resolved views render as islands, client-registered ones mount into
/// <c>#callora-app</c>.
/// </para>
/// <para>
/// A template plugin that renders full SSR HTML replaces this entirely via its own entry
/// template (see PublishedSurfaceTemplateBundles), and can itself extend the same bundle.
/// </para>
/// </summary>
public static class SurfaceShellTemplates
{
    /// <summary>
    /// The host's default surface page — see <c>Resources/views/surface/page/app.njk</c>.
    /// <para>
    /// Deliberately not a <c>const</c>: a const's VALUE is part of the public API and gets
    /// compiled into every consumer, which would make each edit to the shell a breaking
    /// change. What the host renders by default is implementation.
    /// </para>
    /// </summary>
    public static readonly string SpaRoot = """{% extends "@callora/page/app.njk" %}""";

    /// <summary>
    /// The page for a surface whose layout was composed in the editor — see
    /// <c>Resources/views/surface/page/composed.njk</c>. The sections and blocks are already
    /// markup by the time it renders; the template only decides where they sit.
    /// </summary>
    public static readonly string Composed = """{% extends "@callora/page/composed.njk" %}""";
}
