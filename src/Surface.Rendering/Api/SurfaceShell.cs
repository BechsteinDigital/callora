namespace Callora.Surface.Rendering.Api;

/// <summary>
/// Womit eine aufgelöste Fläche gerendert wird: das Template und die Bundle-Kette, gegen die
/// seine <c>extends</c>/<c>include</c> auflösen.
/// </summary>
/// <remarks>
/// Trägt bewusst KEINE Aussage über Adressierung. Ob eine Fläche Pfade unterhalb ihrer selbst
/// deutet, steht an der Fläche (<c>SurfaceRouting</c>) — ein Server-Template ist kein Router,
/// und eine Anwendung braucht durchgereichte Unterpfade auch ohne eigenes Template.
/// </remarks>
internal sealed record SurfaceShell(string Template, IReadOnlyList<string> Chain);
