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
public sealed class SnippetStringLocalizerFactory(ISnippetCatalog catalog) : IStringLocalizerFactory
{
    public IStringLocalizer Create(Type resourceSource) => new SnippetStringLocalizer(catalog);

    public IStringLocalizer Create(string baseName, string location) => new SnippetStringLocalizer(catalog);
}
