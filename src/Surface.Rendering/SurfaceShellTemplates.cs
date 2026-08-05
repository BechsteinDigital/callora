namespace Callora.Surface.Rendering;

/// <summary>
/// The built-in minimal SurfaceShell (ADR-014 §8.1/§11.2): a single app root that
/// carries the workspace/surface context and loads the colocated surface runtime
/// (Resources/app/surface → wwwroot/surface-app). The runtime is the neutral
/// grundgerüst — it ships no UI of its own; every concrete surface comes from a
/// plugin registering against it. A template plugin that renders full SSR HTML
/// replaces this shell entirely via its own entry template (see PublishedSurfaceTemplateBundles).
/// </summary>
public static class SurfaceShellTemplates
{
    /// <summary>SPA-root document — one mount point, no fixed navigation/CMS; boots the surface runtime.</summary>
    public const string SpaRoot =
        """
        <!doctype html>
        <html lang="{{ locale }}">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>{{ surface.key }}</title>
          <link rel="stylesheet" href="/surface-app/surface.css" />
        </head>
        <body>
          <div id="callora-app"
               data-workspace="{{ workspace.key }}"
               data-surface="{{ surface.key }}"
               data-caller-state="{{ caller.state }}"
               data-caller-issuer="{{ caller.issuer }}"
               data-caller-subject="{{ caller.subjectId }}"
               data-caller-name="{{ caller.displayName }}"
               data-caller-claims="{{ caller.claimsJson }}"></div>
          <script src="/surface-app/surface.js" defer></script>
        </body>
        </html>
        """;
}
