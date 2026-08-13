using Callora.Core.Application.Plugins;
using Callora.Core.Tests.Support;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

/// <summary>
/// Die Brücke zwischen dem Manifest und dem Planer. Sie war einseitig: IsFoundation stand hart auf
/// false, und PluginPackageRegistryMetadata führte gar kein Tier-Feld — der Leser parste die
/// Angabe aus der registry.json und ließ sie fallen. „Foundation zuerst" war damit im
/// Produktivpfad ein toter Parameter, der Sortierschlüssel des Planers fiel auf den Eingabeindex
/// zusammen.
/// </summary>
public sealed class PluginActivationOrderingTests
{
    [Fact]
    public async Task ASystemTierPluginIsActivatedBeforeAnApplicationTierPlugin()
    {
        var reader = new StaticPluginPackageRegistryReader();
        reader.AddMetadata("/plugins/app.dll", tier: "application");
        reader.AddMetadata("/plugins/foundation.dll", tier: "system");

        // Die Foundation steht ABSICHTLICH hinten in der Eingabe: Stünde sie vorn, bewiese ein
        // Treffer nichts — der Eingabeindex allein hätte dieselbe Reihenfolge ergeben.
        var order = await PluginActivationOrdering.OrderAsync(
            [("app", "/plugins/app.dll"), ("foundation", "/plugins/foundation.dll")],
            reader,
            CancellationToken.None);

        Assert.Equal(["foundation", "app"], order);
    }

    /// <summary>
    /// Ohne Angabe bleibt es beim bisherigen Verhalten: Application ist der Vorgabewert, die
    /// Eingabereihenfolge entscheidet. Der Fix soll die Reihenfolge dort, wo niemand etwas
    /// deklariert, nicht umbauen.
    /// </summary>
    [Fact]
    public async Task WithoutADeclaredTierTheInputOrderIsKept()
    {
        var reader = new StaticPluginPackageRegistryReader();
        reader.AddMetadata("/plugins/a.dll");
        reader.AddMetadata("/plugins/b.dll");

        var order = await PluginActivationOrdering.OrderAsync(
            [("b", "/plugins/b.dll"), ("a", "/plugins/a.dll")],
            reader,
            CancellationToken.None);

        Assert.Equal(["b", "a"], order);
    }

    /// <summary>
    /// Die Vorliebe ist eine Vorliebe, keine Umgehung: Erzwingt eine Capability-Kante eine
    /// Reihenfolge, gilt sie — auch gegen die Stufe. Ein System-Plugin, das auf eine Capability
    /// eines Application-Plugins wartet, kann nicht vorher starten.
    /// </summary>
    [Fact]
    public async Task ACapabilityEdgeStillOutranksTheTierPreference()
    {
        var reader = new StaticPluginPackageRegistryReader();
        reader.AddMetadata("/plugins/provider.dll", tier: "application", capabilities: ["storage"]);
        reader.AddMetadata("/plugins/foundation.dll", tier: "system", requiredCapabilities: ["storage"]);

        var order = await PluginActivationOrdering.OrderAsync(
            [("foundation", "/plugins/foundation.dll"), ("provider", "/plugins/provider.dll")],
            reader,
            CancellationToken.None);

        Assert.Equal(["provider", "foundation"], order);
    }

    /// <summary>
    /// Ohne Leser bleibt die Eingabe unangetastet — der Weg, den ein Host ohne Manifest-Leser geht.
    /// </summary>
    [Fact]
    public async Task WithoutARegistryReaderTheInputIsReturnedUnchanged()
    {
        var order = await PluginActivationOrdering.OrderAsync(
            [("b", "/plugins/b.dll"), ("a", "/plugins/a.dll")],
            registryReader: null,
            CancellationToken.None);

        Assert.Equal(["b", "a"], order);
    }
}
