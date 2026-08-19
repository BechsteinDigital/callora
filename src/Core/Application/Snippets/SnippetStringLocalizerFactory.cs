using Microsoft.Extensions.Localization;

namespace Callora.Core.Application.Snippets;

/// <summary>
/// Reicht jedem Aufrufer denselben Localizer der laufenden Anfrage (ADR-024 §2 Punkt 7).
/// </summary>
/// <remarks>
/// Typ und Ressourcenort werden bewusst ignoriert: Ein Snippet-Schlüssel trägt sein Paket schon
/// als Präfix, und eine zweite Zuordnung daneben wäre eine zweite Wahrheit darüber, wem ein Text
/// gehört.
/// </remarks>
/// <remarks>
/// <para>
/// Der Katalog kommt als Delegat, weil diese Factory ein SINGLETON sein muss. ASP.NET löst
/// <see cref="IStringLocalizerFactory"/> beim Aufbau der MvcOptions auf — aus der Wurzel des
/// Containers, nicht aus einem Anfrage-Scope. Als scoped registriert verhinderte sie den Start
/// des gesamten Hosts: "Cannot consume scoped service 'ISnippetCatalog' from singleton
/// 'IOptions&lt;MvcOptions&gt;'", Prozessende mit Code 134, bevor ein Port offen war.
/// </para>
/// <para>
/// Wer den Delegaten stellt, entscheidet die Registrierung; sie kennt ASP.NET, diese Schicht
/// nicht. Damit bleibt die Zusage des Typs erhalten — der Localizer sieht den Katalog DER
/// laufenden Anfrage — ohne dass hier ein HttpContext auftaucht.
/// </para>
/// </remarks>
public sealed class SnippetStringLocalizerFactory(Func<ISnippetCatalog?> catalog) : IStringLocalizerFactory
{
    public IStringLocalizer Create(Type resourceSource) => new SnippetStringLocalizer(catalog);

    public IStringLocalizer Create(string baseName, string location) => new SnippetStringLocalizer(catalog);
}
