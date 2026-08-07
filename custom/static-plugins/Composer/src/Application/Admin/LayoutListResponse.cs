namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>Ein Layout in der Übersicht des Editors.</summary>
/// <param name="LayoutKey">Womit der Entwurf geladen wird.</param>
/// <param name="Name">Was angezeigt wird.</param>
/// <param name="SurfaceKey">
/// Die Fläche, die es rendert, oder null. Ein Layout darf gebaut werden, bevor jemand
/// entscheidet, wo es hingeht — deshalb kein Ersatzwert: „default" hier sähe aus wie eine
/// Zuordnung, die niemand getroffen hat.
/// </param>
/// <param name="HasPublishedVersion">
/// Ob Besucher es sehen. Der Unterschied zwischen „gebaut" und „veröffentlicht" ist der, den ein
/// Editor am ehesten übersieht.
/// </param>
public sealed record LayoutListResponse(
    string LayoutKey,
    string Name,
    string? SurfaceKey,
    bool HasPublishedVersion);
