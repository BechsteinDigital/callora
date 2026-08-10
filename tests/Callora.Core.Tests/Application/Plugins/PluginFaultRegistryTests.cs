using Callora.Core.Application.Plugins;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

/// <summary>
/// Das Fehlerbudget je Plugin: Es zählt Fehler in einem gleitenden Fenster, nimmt einem Plugin
/// beim Überschreiten die Verfügbarkeit, meldet den Übergang genau einmal und gibt sie zurück,
/// sobald das Fenster leer läuft. Die Zeit treibt ein <see cref="FakeTimeProvider"/>, damit das
/// Fenster deterministisch ist.
/// </summary>
/// <remarks>
/// Warum das gebraucht wird: Ein Plugin, das beim Aktivieren scheitert, wird Faulted und fällt
/// über den RuntimeHealthy-Faktor aus der Verfügbarkeit. Ein Plugin, das AKTIV ist und bei jeder
/// Anfrage wirft, tut das nicht — es blieb unbegrenzt verfügbar und riss jede Anfrage mit, bis
/// jemand es von Hand deaktivierte. In einem Prozess, den sich alle teilen, ist das die
/// Fehlerklasse, die andere Teams trifft.
/// </remarks>
public sealed class PluginFaultRegistryTests
{
    private const int Threshold = 3;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    [Fact]
    public void AnUnknownPlugin_IsWithinBudget()
    {
        var (registry, _, _) = NewRegistry();

        Assert.True(registry.IsWithinBudget("comm"));
    }

    [Fact]
    public void FaultsBelowTheThreshold_StayWithinBudget()
    {
        var (registry, _, exceeded) = NewRegistry();

        registry.Record("comm", PluginFaultOrigin.HttpRoute);
        registry.Record("comm", PluginFaultOrigin.HttpRoute);

        Assert.True(registry.IsWithinBudget("comm"));
        Assert.Empty(exceeded);
    }

    [Fact]
    public void ReachingTheThreshold_LeavesTheBudget_AndReportsOnce()
    {
        var (registry, _, exceeded) = NewRegistry();

        for (var i = 0; i < Threshold; i++)
        {
            registry.Record("comm", PluginFaultOrigin.HttpRoute);
        }

        Assert.False(registry.IsWithinBudget("comm"));
        var report = Assert.Single(exceeded);
        Assert.Equal("comm", report.PluginId);
        Assert.Equal(Threshold, report.FaultCount);
    }

    [Fact]
    public void FurtherFaults_AfterExceeding_DoNotReportAgain()
    {
        var (registry, _, exceeded) = NewRegistry();

        for (var i = 0; i < Threshold + 4; i++)
        {
            registry.Record("comm", PluginFaultOrigin.Job);
        }

        Assert.Single(exceeded);
    }

    [Fact]
    public void FaultsOlderThanTheWindow_NoLongerCount()
    {
        var (registry, time, _) = NewRegistry();
        registry.Record("comm", PluginFaultOrigin.HttpRoute);
        registry.Record("comm", PluginFaultOrigin.HttpRoute);

        // Die beiden ersten fallen aus dem Fenster, bevor der dritte kommt.
        time.Advance(Window + TimeSpan.FromSeconds(1));
        registry.Record("comm", PluginFaultOrigin.HttpRoute);

        Assert.True(registry.IsWithinBudget("comm"));
    }

    [Fact]
    public void AfterTheWindowElapses_TheBudgetHealsItself()
    {
        var (registry, time, _) = NewRegistry();
        for (var i = 0; i < Threshold; i++)
        {
            registry.Record("comm", PluginFaultOrigin.Event);
        }

        Assert.False(registry.IsWithinBudget("comm"));

        time.Advance(Window + TimeSpan.FromSeconds(1));

        // Selbstheilung ist Absicht: Ein Plugin, dessen Ursache behoben ist — eine wieder
        // erreichbare Gegenstelle etwa —, soll ohne Eingriff zurückkommen. Sonst wäre das
        // Budget eine stille Deaktivierung, die niemand bemerkt und niemand zurücknimmt.
        Assert.True(registry.IsWithinBudget("comm"));
    }

    [Fact]
    public void RecoveringAndExceedingAgain_ReportsAgain()
    {
        var (registry, time, exceeded) = NewRegistry();
        for (var i = 0; i < Threshold; i++)
        {
            registry.Record("comm", PluginFaultOrigin.Event);
        }

        time.Advance(Window + TimeSpan.FromSeconds(1));
        for (var i = 0; i < Threshold; i++)
        {
            registry.Record("comm", PluginFaultOrigin.Event);
        }

        Assert.Equal(2, exceeded.Count);
    }

    [Fact]
    public void PluginsAreCountedSeparately()
    {
        var (registry, _, _) = NewRegistry();

        for (var i = 0; i < Threshold; i++)
        {
            registry.Record("comm", PluginFaultOrigin.HttpRoute);
        }

        registry.Record("composer", PluginFaultOrigin.HttpRoute);

        Assert.False(registry.IsWithinBudget("comm"));
        Assert.True(registry.IsWithinBudget("composer"));
    }

    [Fact]
    public void PluginIdsAreMatchedCaseInsensitively()
    {
        var (registry, _, _) = NewRegistry();

        for (var i = 0; i < Threshold; i++)
        {
            registry.Record("Comm", PluginFaultOrigin.HttpRoute);
        }

        Assert.False(registry.IsWithinBudget("comm"));
    }

    [Fact]
    public void Clear_RestoresTheBudget()
    {
        var (registry, _, _) = NewRegistry();
        for (var i = 0; i < Threshold; i++)
        {
            registry.Record("comm", PluginFaultOrigin.Lifecycle);
        }

        // Wer ein Plugin neu aktiviert, fängt bei null an — sonst schlüge ein Budget aus der
        // vorigen Fassung sofort wieder zu.
        registry.Clear("comm");

        Assert.True(registry.IsWithinBudget("comm"));
    }

    [Fact]
    public void AThresholdOfZero_DisablesTheBudget()
    {
        var (registry, _, exceeded) = NewRegistry(threshold: 0);

        for (var i = 0; i < 50; i++)
        {
            registry.Record("comm", PluginFaultOrigin.HttpRoute);
        }

        Assert.True(registry.IsWithinBudget("comm"));
        Assert.Empty(exceeded);
    }

    [Fact]
    public void TheReport_NamesTheOriginsThatContributed()
    {
        var (registry, _, exceeded) = NewRegistry();

        registry.Record("comm", PluginFaultOrigin.HttpRoute);
        registry.Record("comm", PluginFaultOrigin.Job);
        registry.Record("comm", PluginFaultOrigin.HttpRoute);

        var report = Assert.Single(exceeded);
        Assert.Contains(PluginFaultOrigin.HttpRoute, report.Origins);
        Assert.Contains(PluginFaultOrigin.Job, report.Origins);
    }

    private static (PluginFaultRegistry Registry, FakeTimeProvider Time, List<PluginFaultBudgetExceeded> Exceeded)
        NewRegistry(int threshold = Threshold)
    {
        var time = new FakeTimeProvider();
        var registry = new PluginFaultRegistry(threshold, Window, time);
        var exceeded = new List<PluginFaultBudgetExceeded>();
        registry.BudgetExceeded += report => exceeded.Add(report);
        return (registry, time, exceeded);
    }
}
