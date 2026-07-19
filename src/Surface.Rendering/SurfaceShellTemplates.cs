namespace Callora.Surface.Rendering;

/// <summary>
/// The built-in minimal SurfaceShell (ADR-014 §8.1). For phase E1 the only
/// shipped surface is the SPA-root document (§11.2): the shell renders a single
/// app root carrying the workspace/surface context for a client-side SPA. Later
/// phases (E2/E3) replace this with bundle-loaded, block-composed templates.
/// </summary>
public static class SurfaceShellTemplates
{
    /// <summary>SPA-root document — one mount point, no fixed navigation/CMS.</summary>
    public const string SpaRoot =
        """
        <!doctype html>
        <html lang="{{ locale }}">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>{{ surface.key }}</title>
        </head>
        <body>
          <div id="callora-app" data-workspace="{{ workspace.key }}" data-surface="{{ surface.key }}"></div>
        </body>
        </html>
        """;
}
