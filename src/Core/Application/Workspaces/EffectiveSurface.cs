using Callora.Core.Domain.Workspaces;

namespace Callora.Core.Application.Workspaces;

/// <summary>
/// Was für einen Surface-Knoten tatsächlich gilt — der eigene Wert, sonst der des nächsten
/// Vorfahren, der einen setzt (ADR-019 §3).
/// <para>
/// Getrennt von <see cref="WorkspaceSurfaceSnapshot"/>, weil beide verschiedene Fragen
/// beantworten. Der Snapshot sagt, was in der Verwaltung an DIESEM Knoten steht — sonst könnte
/// eine Oberfläche einen geerbten Wert nicht von einem eigenen unterscheiden und würde beim
/// Speichern aus der Vererbung eine Kopie machen. Diese Sicht sagt, was der Renderpfad benutzt.
/// </para>
/// </summary>
public sealed record EffectiveSurface(
    Guid Id,
    string WorkspaceKey,
    string SurfaceKey,
    string? PublicHost,
    string PublicPathPrefix,
    SurfaceAccessMode AccessMode,
    string? Locale,
    string? TemplatePluginId,
    string? TemplateVersion,
    string? ThemePluginId,
    string? ThemeVersion,
    string? IdentityPluginId,
    string? IdentityVersion,
    DateTimeOffset? IdentityAssignedAtUtc)
{
    /// <summary>
    /// Die Wurzel dieses Knotens — er selbst, wenn er keinen Elternteil hat.
    /// <para>
    /// Die Anwendungsgrenze: Anmeldung, Navigation und Vererbung enden hier. Zwei Knoten mit
    /// derselben Wurzel gehören zur selben Anwendung, zwei mit verschiedenen nicht — auch wenn
    /// sie im selben Workspace liegen.
    /// </para>
    /// </summary>
    public required Guid RootId { get; init; }

    /// <summary>
    /// Baut die effektive Sicht aus der Kette (Knoten zuerst, Wurzel zuletzt).
    /// <para>
    /// Der Access Mode ist die eine Ausnahme von „erster gesetzter Wert gewinnt": Er ist nicht
    /// nullbar, also gibt es kein „nicht gesetzt". Es gilt der des Knotens — womit er in beide
    /// Richtungen überschreibbar ist. Das ist Absicht (§3.1): Ein öffentliches Impressum unter
    /// einem angemeldeten Portal ist genauso legitim wie ein geschützter Partnerbereich unter
    /// einer offenen Website.
    /// </para>
    /// <para>
    /// Der Identity-Provider kommt ausschließlich von der Wurzel (§4). Ihn entlang der Kette zu
    /// suchen ließe eine Anmeldung mitten im Baum enden, ohne dass die URL es verriete.
    /// </para>
    /// </summary>
    public static EffectiveSurface From(IReadOnlyList<WorkspaceSurface> ancestry)
    {
        ArgumentNullException.ThrowIfNull(ancestry);
        if (ancestry.Count == 0)
        {
            throw new ArgumentException("Die Kette enthält keinen Knoten.", nameof(ancestry));
        }

        var node = ancestry[0];
        var root = ancestry[^1];

        // Plugin-Id und Version kommen VOM SELBEN Knoten. Einzeln gesucht ergäben sie im
        // schlimmsten Fall das Theme des einen Vorfahren mit der Version eines anderen — eine
        // Zuweisung, die es nie gab, und ein Fehler, den niemand beim Lesen des Codes sähe.
        var themeFrom = SurfaceTree.InheritedFrom(ancestry, surface => surface.ThemePluginId);
        var templateFrom = SurfaceTree.InheritedFrom(ancestry, surface => surface.TemplatePluginId);

        return new EffectiveSurface(
            node.Id,
            node.Workspace.WorkspaceKey,
            node.SurfaceKey,
            SurfaceTree.Inherited(ancestry, surface => surface.PublicHost),
            SurfaceTree.ComposePath(ancestry.Select(surface => surface.PublicPathPrefix).ToArray()),
            node.AccessMode,
            SurfaceTree.Inherited(ancestry, surface => surface.Locale),
            templateFrom?.TemplatePluginId,
            templateFrom?.TemplateVersion,
            themeFrom?.ThemePluginId,
            themeFrom?.ThemeVersion,
            root.IdentityPluginId,
            root.IdentityVersion,
            root.IdentityAssignedAtUtc)
        {
            RootId = root.Id,
        };
    }
}
