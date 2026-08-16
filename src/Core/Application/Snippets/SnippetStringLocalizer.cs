using Microsoft.Extensions.Localization;

namespace Callora.Core.Application.Snippets;

/// <summary>
/// Der Standardvertrag für Plugins: <see cref="IStringLocalizer"/> über den Katalog der Anfrage
/// (ADR-024 §2 Punkt 7).
/// </summary>
/// <remarks>
/// Plugin-Autoren kennen ihn aus jedem ASP.NET-Projekt; der Unterschied zu einem eigenen
/// Callora-Port wäre im Code gering und beim Einarbeiten groß. Die ganze Auflösungskette bleibt
/// hinter der Factory — ein Plugin sieht nur den Standard.
///
/// <para>
/// Ein fehlender Schlüssel gibt den Namen zurück und meldet das über
/// <see cref="LocalizedString.ResourceNotFound"/>, wie der Vertrag es vorsieht. Der Aufrufer kann
/// damit seinen eingebauten Text zeigen, statt einen Schlüssel — das ist die Grundlage der
/// schrittweisen Migration der Oberflächen.
/// </para>
/// </remarks>
public sealed class SnippetStringLocalizer(ISnippetCatalog catalog) : IStringLocalizer
{
    public LocalizedString this[string name] => Lookup(name);

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var found = Lookup(name);
            return new LocalizedString(
                name,
                string.Format(System.Globalization.CultureInfo.CurrentCulture, found.Value, arguments),
                found.ResourceNotFound);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        catalog.Snippets.Select(entry => new LocalizedString(entry.Key, entry.Value, resourceNotFound: false));

    private LocalizedString Lookup(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return catalog.Snippets.TryGetValue(name, out var value)
            ? new LocalizedString(name, value, resourceNotFound: false)
            : new LocalizedString(name, name, resourceNotFound: true);
    }
}
