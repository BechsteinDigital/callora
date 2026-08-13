using Callora.Core.Application.Plugins;
using System.Reflection;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

/// <summary>
/// Was der Betreiber sieht, wenn ein Plugin gegen eine andere Fassung eines Vertrags gebaut wurde.
/// </summary>
/// <remarks>
/// <para>
/// Der Anlass (#283): <c>ISurfaceLayoutSource.GetDraftAsync</c> bekam einen Parameter, und ein
/// dagegen gebautes Plugin ließ sich danach nicht mehr vollständig laden. Sichtbar wurde das nicht
/// als abgelehntes Plugin, sondern als fehlender Editor in der Oberfläche — die Ursache stand in
/// einer Logzeile, die es nur gab, weil jemand sie aus einem ähnlichen Anlass eingebaut hatte.
/// </para>
/// <para>
/// Geprüft wird deshalb nicht, DASS eine Meldung kommt, sondern dass sie die drei Dinge enthält, an
/// denen jemand weiterkommt: welches Paket, wie viele Typen betroffen sind, und dass neu gebaut
/// werden muss. Die Meldung des Typladers allein nennt keines davon.
/// </para>
/// </remarks>
public sealed class AContractBreakIsNamedAtLoadTests
{
    [Fact]
    public void TheMessageNamesThePackageTheCountAndTheRemedy()
    {
        var exception = BreakOf(
            brokenTypes: 1,
            healthyTypes: 2,
            "Method 'GetDraftAsync' in type 'Callora.Plugin.Composer.Application.ComposerLayoutSource' "
            + "from assembly 'Callora.Plugin.Composer' does not have an implementation.");

        var message = PluginContractBreakDiagnostics.Describe(exception, "Callora.Plugin.Composer");

        Assert.Contains("Callora.Plugin.Composer", message, StringComparison.Ordinal);
        Assert.Contains("1 von 3", message, StringComparison.Ordinal);
        Assert.Contains("GetDraftAsync", message, StringComparison.Ordinal);
        Assert.Contains("neu gebaut", message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Mehrere gebrochene Typen ergeben mehrere Meldungen des Typladers. Alle zu nennen erspart die
    /// Kette aus Neubau, Neustart und nächster Meldung.
    /// </summary>
    [Fact]
    public void EveryBrokenTypeIsNamed()
    {
        var exception = BreakOf(
            brokenTypes: 2,
            healthyTypes: 1,
            "Method 'GetDraftAsync' in type 'A' does not have an implementation.",
            "Method 'PublishAsync' in type 'B' does not have an implementation.");

        var message = PluginContractBreakDiagnostics.Describe(exception, "Some.Plugin");

        Assert.Contains("GetDraftAsync", message, StringComparison.Ordinal);
        Assert.Contains("PublishAsync", message, StringComparison.Ordinal);
        Assert.Contains("2 von 3", message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ein Typlader, der keinen Grund mitgibt, darf nicht in eine Meldung münden, die so aussieht,
    /// als stünde dort einer.
    /// </summary>
    [Fact]
    public void AnExceptionWithoutReasonsStillSaysSomethingUsable()
    {
        var exception = new ReflectionTypeLoadException([null, typeof(string)], [null!, null!]);

        var message = PluginContractBreakDiagnostics.Describe(exception, "Some.Plugin");

        Assert.Contains("Some.Plugin", message, StringComparison.Ordinal);
        Assert.Contains("keinen Grund", message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Die Gegenprobe am echten Weg: Eine Assembly, deren Typen alle laden, ergibt keine Meldung.
    /// Ohne sie bestünde der Rest auch dann, wenn <c>Describe</c> immer etwas zurückgäbe.
    /// </summary>
    [Fact]
    public void AHealthyAssemblyProducesNoMessage()
    {
        Assert.Null(PluginContractBreakDiagnostics.Describe(typeof(PluginContractBreakDiagnostics).Assembly));
    }

    private static ReflectionTypeLoadException BreakOf(int brokenTypes, int healthyTypes, params string[] reasons)
    {
        // Die Typenliste bildet nach, was der Lader liefert: null je nicht ladbarem Typ, der Typ
        // selbst für die übrigen. Genau daraus liest die Meldung „x von y".
        var types = new Type?[brokenTypes + healthyTypes];
        for (var i = brokenTypes; i < types.Length; i++)
        {
            types[i] = typeof(string);
        }

        var exceptions = reasons.Select(reason => (Exception?)new TypeLoadException(reason)).ToArray();
        return new ReflectionTypeLoadException(types, exceptions);
    }
}
