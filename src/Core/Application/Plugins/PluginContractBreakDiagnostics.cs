using System.Reflection;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Erkennt beim Laden, dass ein Plugin gegen eine andere Fassung eines Vertrags gebaut wurde als
/// die, die der Host bereitstellt — und sagt es so, dass der Betreiber weiß, was zu tun ist.
/// </summary>
/// <remarks>
/// <para>
/// Der Anlass steht in #283: <c>ISurfaceLayoutSource.GetDraftAsync</c> bekam einen Parameter, und
/// ein dagegen gebautes Plugin ließ sich danach nicht mehr vollständig laden. Sichtbar wurde das
/// nicht als abgelehntes Plugin, sondern als fehlender Editor in der Oberfläche — die Ursache stand
/// in einer Logzeile, und die gab es nur, weil jemand sie aus einem ähnlichen Anlass eingebaut hatte.
/// </para>
/// <para>
/// Der Hebel ist <see cref="Assembly.GetTypes"/>: Es wirft
/// <see cref="ReflectionTypeLoadException"/>, sobald ein Typ nicht geladen werden kann, und zwar
/// <b>ohne dass irgendetwas instanziiert wird</b>. Gemessen an einer eigens gebrochenen Assembly
/// liefert es dabei zweierlei: die Ausnahme je kaputtem Typ und in <c>ex.Types</c> die Typen, die
/// heil sind. Genau das erklärt den Vorfall — der Einstiegspunkt lud sauber, das Plugin startete,
/// und nur der eine Typ fehlte. Ein Plugin gilt hier trotzdem als kaputt: Ein Vertrag, den ein Teil
/// des Pakets nicht mehr erfüllt, ist kein Teilschaden, sondern eine Fassung, die nicht passt.
/// </para>
/// <para>
/// Übersetzt wird, weil die Laufzeitmeldung („Method 'X' in type 'Y' does not have an
/// implementation") beschreibt, was der Lader sieht, und nicht, was der Betreiber tun soll. Sie
/// nennt weder den Vertrag noch dessen Fassung noch das Wort „neu bauen".
/// </para>
/// </remarks>
public static class PluginContractBreakDiagnostics
{
    /// <summary>
    /// Prüft die Typen der Plugin-Assembly und liefert eine Meldung, wenn einer davon nicht zu den
    /// geladenen Verträgen passt. <see langword="null"/> heißt: alles ladbar.
    /// </summary>
    /// <param name="pluginAssembly">Die bereits geladene Plugin-Assembly.</param>
    public static string? Describe(Assembly pluginAssembly)
    {
        ArgumentNullException.ThrowIfNull(pluginAssembly);

        try
        {
            pluginAssembly.GetTypes();
            return null;
        }
        catch (ReflectionTypeLoadException exception)
        {
            return Describe(exception, pluginAssembly.GetName().Name);
        }
    }

    /// <summary>
    /// Formuliert die Meldung aus einer bereits gefangenen Ausnahme.
    /// </summary>
    /// <param name="exception">Was der Typlader gemeldet hat.</param>
    /// <param name="assemblyName">Name der Plugin-Assembly, für die Meldung.</param>
    public static string Describe(ReflectionTypeLoadException exception, string? assemblyName)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // Mehrere kaputte Typen ergeben mehrere Ausnahmen. Alle zu nennen ist hier richtig und nicht
        // nur gründlich: Wer eine Fassung zurückportiert, will beim ersten Versuch wissen, wie viele
        // Stellen es sind — sonst wird daraus eine Kette aus Neubau, Neustart, nächster Meldung.
        var reasons = exception.LoaderExceptions
            .OfType<Exception>()
            .Select(static loader => loader.Message.Trim())
            .Where(static message => message.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var loadable = exception.Types.Count(static type => type is not null);
        var total = exception.Types.Length;

        var detail = reasons.Length == 0
            ? "Der Typlader nennt keinen Grund."
            : string.Join(" ", reasons);

        return $"Plugin '{assemblyName ?? "?"}' passt nicht zu den geladenen Verträgen: "
            + $"{total - loadable} von {total} Typen ließen sich nicht laden. {detail} "
            + "Das Paket wurde gegen eine andere Fassung eines Vertrags gebaut als die, die dieser "
            + "Host bereitstellt, und muss dagegen neu gebaut werden.";
    }
}
