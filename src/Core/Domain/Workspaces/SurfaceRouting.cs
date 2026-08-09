namespace Callora.Core.Domain.Workspaces;

/// <summary>
/// Wer über die Adressen unterhalb dieser Fläche entscheidet (ADR-022).
/// </summary>
/// <remarks>
/// Eine eigene Achse, nicht aus <see cref="WorkspaceSurface.SurfaceType"/> abgeleitet: Der Typ
/// ist beschreibend und frei — auch ein Plugin trägt dort ein, was es für richtig hält. Aus
/// einem freien Wort auf das Routingverhalten zu schließen hieße, jeden Wert zu kennen, den
/// morgen jemand erfindet.
///
/// <para>
/// Ebensowenig aus dem Renderweg: Ob ein Plugin ein eigenes Server-Template mitbringt, sagt
/// nichts darüber, ob es Pfade deutet. Ein Template ist kein Router, und eine SPA mit
/// History-Routing braucht durchgereichte Unterpfade ganz ohne Server-Template.
/// </para>
///
/// <para>
/// <b>Nicht vererbt.</b> Jeder Knoten beantwortet die Frage für sich. Ein geerbtes
/// <see cref="Application"/> machte still jeden Tippfehler unter einem ganzen Teilbaum zu einer
/// 200 — genau die Weitergabe, die den Fehler erzeugt hat.
/// </para>
/// </remarks>
public enum SurfaceRouting
{
    /// <summary>
    /// Der Baum ist die Wahrheit: Was kein Knoten ist, gibt es nicht. Ein Pfad unterhalb dieser
    /// Fläche, der keinem Kind entspricht, ist ein 404 — wie ein Tippfehler in jedem Shop.
    /// <para>Der Normalfall für Websites, Portale und alles, was der Composer gestaltet.</para>
    /// </summary>
    Tree = 0,

    /// <summary>
    /// Die Anwendung deutet ihre Unterpfade selbst. <c>/raeume/abc123</c> ist keine Seite im
    /// Baum, sondern eine Instanz, die zur Laufzeit entsteht — sie kann gar nicht als Knoten
    /// angelegt worden sein.
    /// <para>
    /// Der Renderweg bleibt derselbe: Die Anwendung darf njk und Inseln benutzen, damit sie
    /// unter demselben Theme steht wie der Rest. Das ist der Punkt, an dem White-Label steht
    /// oder fällt.
    /// </para>
    /// </summary>
    Application = 1,
}
