using System.Text.RegularExpressions;

namespace Callora.Core.Application.Monitoring;

/// <summary>
/// Entschärft eine Browser-Meldung, bevor sie ins Betriebslog geht (#294).
/// </summary>
/// <remarks>
/// Der Inhalt wird nicht ausgewertet — was der Browser meldet, ist seine Aussage, nicht unsere.
/// Entschärft wird trotzdem, und zwar an den drei Stellen, an denen eine fremde Zeichenkette in
/// einem Log Schaden anrichtet:
///
/// <list type="number">
/// <item>
/// <b>Query-Strings.</b> Ein Stacktrace trägt die URLs mit, an denen er entstanden ist. Deren
/// Query ist der Ort, an dem auf einer öffentlichen Fläche personenbezogene Daten stehen — eine
/// Mailadresse, ein Termin-Token. Sie fällt weg, in der gemeldeten URL strukturiert und im
/// Freitext an der URL-artigen Sequenz. Ein Fragezeichen im Fließtext bleibt, wo es steht: Wer
/// das nicht unterscheidet, verstümmelt die Meldung, die er lesbar halten wollte.
/// </item>
/// <item>
/// <b>Steuerzeichen.</b> Ein <c>\n</c> im Text, und was danach kommt, liest sich im Log wie ein
/// Eintrag des Systems. Ein Absender, der sich eigene Logzeilen schreiben kann, macht das Log als
/// Beleg wertlos.
/// </item>
/// <item>
/// <b>Länge.</b> Harte Grenzen je Feld, damit eine einzelne Meldung kein Logziel füllt. Was
/// darüber steht, wird abgeschnitten und mit einem Auslassungszeichen als abgeschnitten kenntlich
/// gemacht.
/// </item>
/// </list>
///
/// Die Herkunft ist kein Freitext, sondern eine von zwei bekannten Angaben: Sie landet in einem
/// Logfeld, nach dem jemand filtert, und alles andere wäre wieder eine Zeichenkette des Absenders
/// an einer Stelle, an der sie etwas bedeutet.
/// </remarks>
public static class ClientErrorSanitizer
{
    public const int MaxMessageLength = 300;
    public const int MaxStackLength = 4_000;
    public const int MaxUrlLength = 500;

    private const string UnknownSource = "unknown";

    private static readonly string[] KnownSources = ["admin", "surface"];

    // Die Query hängt an einer URL-artigen Sequenz — http(s)://… bis zum ersten Zeichen, das in
    // einem Stackframe die URL beendet. Nur dort wird geschnitten.
    //
    // Der Positionssuffix (`:12:3`) bleibt: Er steht in jedem Stackframe HINTER der URL und ist
    // der Teil, der einen Stacktrace überhaupt brauchbar macht. Ein Query-Wert, der selbst auf
    // `:12` endet, behält damit dieses Stück — ein Zahlenfragment ohne Schlüssel, und der
    // deutlich kleinere Preis, verglichen mit Stackframes ohne Zeilennummer.
    private static readonly Regex UrlQuery = new(
        @"(https?://[^\s)'""]*?)\?[^\s)'""]*?((?::\d+)*)(?=[\s)'""]|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(200));

    public static SanitizedClientError Sanitize(ClientErrorReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new SanitizedClientError(
            ResolveSource(report.Source),
            Clean(report.Message, MaxMessageLength) ?? string.Empty,
            Clean(report.Stack, MaxStackLength),
            CleanUrl(report.Url));
    }

    private static string ResolveSource(string? source)
        => KnownSources.FirstOrDefault(known => string.Equals(known, source?.Trim(), StringComparison.OrdinalIgnoreCase))
           ?? UnknownSource;

    private static string? Clean(string? value, int maxLength)
    {
        if (value is null)
        {
            return null;
        }

        var withoutQueries = UrlQuery.Replace(value, "$1?…$2");
        var withoutControls = new string(withoutQueries.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
        return Truncate(withoutControls.Trim(), maxLength);
    }

    private static string? CleanUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var trimmed = url.Trim();
        // Absolut und relativ kommen beide vor — window.location.href ist das eine, ein
        // Router-Pfad das andere. Geschnitten wird am ersten ? oder #, was für beide stimmt und
        // ohne Uri-Parsing auskommt, das an einer kaputten Eingabe wirft.
        var cut = trimmed.IndexOfAny(['?', '#']);
        var path = cut >= 0 ? trimmed[..cut] : trimmed;
        return Clean(path, MaxUrlLength);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 1), "…");
}
