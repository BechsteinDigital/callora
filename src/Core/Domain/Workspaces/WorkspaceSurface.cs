namespace Callora.Core.Domain.Workspaces;

/// <summary>
/// A node in a workspace's surface tree (ADR-019).
/// <para>
/// A node without a parent is an <b>application root</b> — what ADR-014 §5 called a surface,
/// the Callora counterpart of a Shopware SalesChannel: website, dialer, agent desktop. It
/// carries the access itself: host or path prefix, access mode, theme, identity provider.
/// </para>
/// <para>
/// A node with a parent is a <b>child</b> — what Shopware calls a category. It inherits the
/// access and overrides only what it needs of its own. Every node may carry a layout, which is
/// the point of the whole thing: there used to be exactly one layout per surface, so a website
/// with three pages would have needed three access surfaces.
/// </para>
/// </summary>
public sealed class WorkspaceSurface
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// The parent node, or null for an application root.
    /// <para>
    /// Always within the same workspace, and never a cycle — both are checked when the value is
    /// set, not when it is resolved. A cycle that surfaces at render time is an endless loop in
    /// the request path.
    /// </para>
    /// </summary>
    public Guid? ParentSurfaceId { get; set; }

    /// <summary>Ascending order among siblings — the order the navigation shows.</summary>
    public int Position { get; set; }

    /// <summary>
    /// Claims, die ein Besucher mitbringen muss, damit dieser Knoten für ihn existiert
    /// (ADR-019 §4) — kommagetrennt, leer heißt: keine Anforderung.
    /// <para>
    /// Kumulativ entlang der Kette und nicht überschreibbar: Was ein Elternteil verlangt, gilt
    /// auch für seine Nachfahren. Anders wäre der Schutz durch Tieferklicken zu umgehen, denn
    /// eine Unterseite hat eine eigene URL.
    /// </para>
    /// <para>
    /// <b>Nicht das Operator-RBAC.</b> Ein Portal-Besucher ist kein Operator; geprüft werden
    /// die Claims seiner Surface-Identität (ADR-017).
    /// </para>
    /// </summary>
    public string? RequiredClaims { get; set; }

    /// <summary>
    /// Claims, die JEDER Besucher dieser Fläche mitbringt — kommagetrennt, leer heißt keine.
    /// </summary>
    /// <remarks>
    /// Die Gegenrichtung zu <see cref="RequiredClaims"/>. Ohne sie hatte ein nicht angemeldeter
    /// Besucher IMMER eine leere Claim-Menge: Jede Ansicht und jeder Block mit einer Anforderung
    /// war auf einer Fläche ohne Identitätsanbieter unerreichbar — auch für einen Gast mit
    /// gültiger Einladung.
    ///
    /// <para>
    /// Kumulativ entlang der Kette, wie die Anforderung: Was ein Elternteil gewährt, gilt auch
    /// hier. Das ist eine Rechteerweiterung, und sie gehört dem Betreiber — dieselbe
    /// Entscheidung wie „ich stelle ein Telefon in die Lobby".
    /// </para>
    /// </remarks>
    public string? GrantedClaims { get; set; }

    /// <summary>Technical key, unique per workspace.</summary>
    public string SurfaceKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Extensible surface-type key (ADR-014 §16), not a closed enum. Default "spa".</summary>
    public string SurfaceType { get; set; } = "spa";

    public string? PublicBaseUrl { get; set; }

    public string? PublicHost { get; set; }

    public string PublicPathPrefix { get; set; } = "/";

    public SurfaceAuthentication Authentication { get; set; } = SurfaceAuthentication.Public;

    /// <summary>
    /// Wer über die Adressen unterhalb dieser Fläche entscheidet.
    /// </summary>
    /// <remarks>
    /// Standard ist <see cref="SurfaceRouting.Tree"/>: Wer nichts sagt, bekommt 404 statt einer
    /// fremden Seite. Ein stiller Default in die andere Richtung liefert unter jedem Tippfehler
    /// 200 mit dem Inhalt der Wurzel — der Fehler, der diese Achse nötig gemacht hat.
    /// </remarks>
    public SurfaceRouting Routing { get; set; } = SurfaceRouting.Tree;

    public string? Locale { get; set; }

    public string? TemplatePluginId { get; set; }

    public string? TemplateVersion { get; set; }

    public string? ThemePluginId { get; set; }

    public string? ThemeVersion { get; set; }

    public string? ThemeAssignedBy { get; set; }

    public DateTimeOffset? ThemeAssignedAtUtc { get; set; }

    /// <summary>
    /// Plugin an operator assigned as this surface's identity provider (ADR-017 §5.2),
    /// or null when the surface has none. Assignment is operator data, not plugin
    /// self-declaration: a shipped login plugin cannot know a surface key the customer
    /// creates later.
    /// </summary>
    public string? IdentityPluginId { get; set; }

    /// <summary>Version of the assigned identity plugin at assignment time.</summary>
    public string? IdentityVersion { get; set; }

    /// <summary>
    /// Who assigned the identity provider. Unlike the theme equivalent this is not a
    /// convenience: who vouches for a surface's visitors, and since when, is audit
    /// material.
    /// </summary>
    public string? IdentityAssignedBy { get; set; }

    /// <summary>
    /// When the identity provider was assigned. Doubles as the invalidation boundary:
    /// a surface session issued before this instant predates the current provider and
    /// is no longer trusted (ADR-017 §6.3).
    /// </summary>
    public DateTimeOffset? IdentityAssignedAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Workspace Workspace { get; set; } = null!;

    /// <summary>
    /// Der Elternknoten als Navigation. Nur zum Lesen des Schlüssels da: Die Verwaltung zeigt
    /// den Elternteil als Schlüssel an, gespeichert ist eine Id — ohne diese Beziehung müsste
    /// jede Projektion eine zweite Abfrage machen oder den Schlüssel weglassen, und weglassen
    /// hieße, dass die API einen Baum ausliefert, in dem niemand den Elternteil sieht.
    /// </summary>
    public WorkspaceSurface? Parent { get; set; }
}
