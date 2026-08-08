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
    /// Die Claims, die dieser Knoten verlangt — die der ganzen Kette zusammen (§4).
    /// <para>
    /// Zusammengeführt und nicht geerbt: Was ein Elternteil fordert, gilt zusätzlich. Nur den
    /// nächsten gesetzten Wert zu nehmen hieße, dass ein Kind ohne eigene Anforderung den
    /// Schutz seines Elternteils aufhebt — und es hat eine eigene URL.
    /// </para>
    /// </summary>
    public required string? RequiredClaims { get; init; }

    /// <summary>
    /// Was jeder Besucher hier mitbringt — die Vereinigung der ganzen Kette.
    /// </summary>
    public required string? GrantedClaims { get; init; }

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
            HostOf(ancestry),
            PathOf(ancestry),
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
            RequiredClaims = string.Join(
                ',',
                ancestry
                    .SelectMany(node => SurfaceVisibility.Parse(node.RequiredClaims))
                    .Distinct(StringComparer.Ordinal)),
            // Dieselbe Kumulation wie bei der Anforderung: Was ein Elternteil gewährt, gilt auch
            // für jede Unterseite. Alles andere zwänge einen Betreiber, dieselbe Gewährung an
            // jedem Knoten zu wiederholen — und jede vergessene wäre eine Seite, die leer bleibt.
            GrantedClaims = string.Join(
                ',',
                ancestry
                    .SelectMany(node => SurfaceVisibility.Parse(node.GrantedClaims))
                    .Distinct(StringComparer.Ordinal)),
        };
    }

    /// <summary>
    /// Der Host dieser Fläche: der eigene oder geerbte, sonst der des Workspaces.
    /// </summary>
    /// <remarks>
    /// Eine Basis-URL kann eine Fläche bezeichnen oder einen Workspace. Die Fläche ist dabei
    /// das speziellere Signal und gewinnt: Wer <c>portal.kunde.de</c> auf eine Fläche legt,
    /// meint diese Fläche, auch wenn der Workspace <c>kunde.de</c> trägt.
    /// </remarks>
    private static string? HostOf(IReadOnlyList<WorkspaceSurface> ancestry) =>
        SurfaceTree.Inherited(ancestry, surface => surface.PublicHost)
        ?? ancestry[0].Workspace?.PublicHost;

    /// <summary>
    /// Der Pfad dieser Fläche, zusammengesetzt aus der Kette — und dem Workspace-Schlüssel
    /// davor, wenn kein Host den Workspace bereits benennt.
    /// </summary>
    /// <remarks>
    /// <c>host.de/&lt;workspace&gt;/&lt;fläche&gt;/&lt;seite&gt;</c> ist der Normalfall nach
    /// dem Anlegen. Ohne das Workspace-Segment beanspruchte jeder Workspace die gesamte
    /// Origin: Zwei frisch angelegte waren nicht unterscheidbar, und der zweite blieb
    /// unerreichbar, ohne dass irgendwo etwas darauf hinwies.
    ///
    /// <para>
    /// Benennt ein Host den Workspace oder die Fläche bereits, entfällt das Segment — es
    /// zweimal zu sagen wäre keine Unterscheidung, sondern eine Wiederholung.
    /// </para>
    ///
    /// <para>
    /// Das Segment steht ANS ENDE des Arrays: ComposePath bekommt die Kette von Knoten zu
    /// Wurzel und dreht sie um.
    /// </para>
    /// </remarks>
    private static string PathOf(IReadOnlyList<WorkspaceSurface> ancestry)
    {
        var segments = ancestry.Select(surface => (string?)surface.PublicPathPrefix).ToList();
        if (HostOf(ancestry) is null)
        {
            segments.Add(ancestry[0].Workspace?.WorkspaceKey);
        }

        return SurfaceTree.ComposePath(segments);
    }
}
