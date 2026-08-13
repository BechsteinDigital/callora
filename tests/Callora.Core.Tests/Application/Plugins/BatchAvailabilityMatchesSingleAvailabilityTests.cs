using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Policies;
using Callora.Core.Domain.Plugins;
using Callora.Core.Tests.Support;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

/// <summary>
/// Die Sammelauswertung darf nichts anderes ergeben als die Einzelauswertung.
/// </summary>
/// <remarks>
/// <para>
/// Das ist die eigentliche Zusage dieser Optimierung: Beschafft wird anders, abgeleitet wird
/// gleich. Verfügbarkeit entscheidet, ob die Oberfläche eines Plugins ausgeliefert wird — eine
/// Abweichung wäre keine Performance-Frage, sondern ein Block, der dort nicht hingehört, oder
/// ein fehlender, den niemand erklären kann.
/// </para>
/// <para>
/// Deshalb vergleichen diese Tests die beiden Wege gegeneinander, statt Erwartungswerte zu
/// wiederholen: Ein Erwartungswert, der zweimal dasteht, wird bei einer Regeländerung einmal
/// vergessen.
/// </para>
/// </remarks>
public sealed class BatchAvailabilityMatchesSingleAvailabilityTests
{
    private const string WorkspaceKey = "workspace-a";
    private const string TenantKey = "tenant-a";

    /// <summary>
    /// Vier Plugins mit unterschiedlichem Ausgang, damit der Vergleich nicht nur den Erfolgsfall
    /// trifft: eines vollständig verfügbar, eines ohne Anspruch, eines mit gestörter Laufzeit,
    /// und eines, das der Host gar nicht kennt.
    /// </summary>
    [Theory]
    [InlineData("plugin-ok")]
    [InlineData("plugin-not-entitled")]
    [InlineData("plugin-faulted")]
    [InlineData("plugin-unknown")]
    public async Task BothPathsAgreeForOnePlugin(string pluginId)
    {
        var evaluator = await CreateAsync();

        var single = await evaluator.EvaluateAsync(pluginId, WorkspaceKey);
        var batch = await evaluator.EvaluateManyAsync([pluginId], WorkspaceKey);

        Assert.True(batch.ContainsKey(pluginId));
        Assert.Equal(single.IsAvailable, batch[pluginId].IsAvailable);
        Assert.Equal(single.UnmetFactors, batch[pluginId].UnmetFactors);
    }

    /// <summary>
    /// Und über die ganze Menge auf einmal — dort könnte der Sammelweg abweichen, weil er
    /// workspaceweite Daten einmal lädt und für alle verwendet.
    /// </summary>
    [Fact]
    public async Task BothPathsAgreeAcrossAllPluginsAtOnce()
    {
        var evaluator = await CreateAsync();
        string[] pluginIds = ["plugin-ok", "plugin-not-entitled", "plugin-faulted", "plugin-unknown"];

        var batch = await evaluator.EvaluateManyAsync(pluginIds, WorkspaceKey);

        foreach (var pluginId in pluginIds)
        {
            var single = await evaluator.EvaluateAsync(pluginId, WorkspaceKey);
            Assert.Equal(single.IsAvailable, batch[pluginId].IsAvailable);
            Assert.Equal(single.UnmetFactors, batch[pluginId].UnmetFactors);
        }
    }

    /// <summary>
    /// Eine doppelt genannte Id ergibt einen Eintrag statt einer Ausnahme. Sonst müsste jeder
    /// Aufrufer vorher entdoppeln — eine Regel, die man vergessen kann und deren Bruch erst im
    /// Betrieb auffällt.
    /// </summary>
    [Fact]
    public async Task DuplicateIdsCollapseIntoOneEntry()
    {
        var evaluator = await CreateAsync();

        var batch = await evaluator.EvaluateManyAsync(
            ["plugin-ok", "plugin-ok", "PLUGIN-OK"],
            WorkspaceKey);

        Assert.Single(batch);
    }

    [Fact]
    public async Task AnEmptyRequestReturnsAnEmptyResult()
    {
        var evaluator = await CreateAsync();

        Assert.Empty(await evaluator.EvaluateManyAsync([], WorkspaceKey));
    }

    private static async Task<PluginAvailabilityEvaluator> CreateAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var installations = new InMemoryPluginInstallationRepository();
        foreach (var pluginId in new[] { "plugin-ok", "plugin-not-entitled", "plugin-faulted" })
        {
            await installations.AddAsync(
                PluginInstallation.CreateInstalled(pluginId, pluginId, $"/plugins/{pluginId}.dll", null, now));
        }

        var lifecycle = new FakeHostPluginLifecycle
        {
            Plugins =
            [
                new HostPluginDescriptor("plugin-ok", "Ok", "/plugins/plugin-ok.dll", null, HostPluginState.Active),
                new HostPluginDescriptor(
                    "plugin-not-entitled", "Not entitled", "/plugins/plugin-not-entitled.dll", null, HostPluginState.Active),
                // Gestörte Laufzeit: der einzige Faktor, der sich ohne Datenbankschreibvorgang
                // ändert — und deshalb der Grund, warum die Kette selbst nicht gecacht werden darf.
                new HostPluginDescriptor(
                    "plugin-faulted", "Faulted", "/plugins/plugin-faulted.dll", null, HostPluginState.Faulted)
            ]
        };

        var entitlements = new InMemoryPluginEntitlementStore(new BackendHostOptions());
        await entitlements.SetEntitledAsync("plugin-ok", isEntitled: true, WorkspaceKey, TenantKey);
        await entitlements.SetEntitledAsync("plugin-faulted", isEntitled: true, WorkspaceKey, TenantKey);

        var activations = new InMemoryWorkspacePluginActivationStore();
        await activations.SetActiveAsync("plugin-ok", WorkspaceKey, TenantKey, isActive: true);
        await activations.SetActiveAsync("plugin-not-entitled", WorkspaceKey, TenantKey, isActive: true);
        await activations.SetActiveAsync("plugin-faulted", WorkspaceKey, TenantKey, isActive: true);

        var workspaces = new InMemoryWorkspaceManagementStore();
        workspaces.AddTenant(TenantKey, isActive: true);
        await workspaces.UpsertAsync(TenantKey, WorkspaceKey, "Workspace A", "standard", isActive: true);

        return new PluginAvailabilityEvaluator(
            installations,
            lifecycle,
            entitlements,
            activations,
            workspaces,
            new PluginCapabilityGuard(installations, activations));
    }
}
