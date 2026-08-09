using Callora.Core.Application.Lifecycle;
using Callora.Core.Domain.Plugins;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

/// <summary>
/// Die Übersicht zeigt, was gilt — nicht nur, was gelten soll.
/// </summary>
/// <remarks>
/// Ein Plugin, dessen Aktivierung beim Start scheitert (eine fehlende Fähigkeit, eine Ausnahme
/// im Start), bleibt in der Datenbank <c>Active</c>. Die Übersicht zeigte es damit als „Aktiv",
/// während es nichts tat; der Fehlschlag stand ausschließlich in einer Logzeile beim Start.
///
/// <para>
/// Abgeleitet statt gespeichert: Der Laufzeitzustand gehört der Laufzeit. Ihn mitzuschreiben
/// hieße, ihn aktuell halten zu müssen — und ein Prozess, der abstürzt, hinterließe eine Zeile,
/// die behauptet, etwas laufe noch.
/// </para>
/// </remarks>
public sealed class AFailedActivationIsVisibleTests
{
    [Fact]
    public void AnInstallationThatIsNotRunningSaysSo()
    {
        // Genau der Fall aus dem Produktionstest: In der Datenbank aktiv, in der Laufzeit nicht.
        var snapshot = Snapshot(state: PluginInstallationState.Active, isRunning: false);

        Assert.Equal((int)PluginInstallationState.Active, snapshot.State);
        Assert.False(snapshot.IsRunning);
    }

    [Fact]
    public void AWorkingPluginReportsBoth()
    {
        var snapshot = Snapshot(state: PluginInstallationState.Active, isRunning: true);

        Assert.Equal((int)PluginInstallationState.Active, snapshot.State);
        Assert.True(snapshot.IsRunning);
    }

    [Fact]
    public void AnInstalledButNeverActivatedPluginIsNotRunningEither()
    {
        // Sieht aus wie ein Fehlschlag und ist keiner: Wer neu entdeckt wurde, wartet auf die
        // Entscheidung des Betreibers. Der Unterschied steht im gewünschten Zustand.
        var snapshot = Snapshot(state: PluginInstallationState.Installed, isRunning: false);

        Assert.Equal((int)PluginInstallationState.Installed, snapshot.State);
        Assert.False(snapshot.IsRunning);
    }

    private static PluginInstallationSnapshot Snapshot(PluginInstallationState state, bool isRunning) =>
        new(
            "videoconference",
            "Video Conference",
            "/plugins/vc.dll",
            null,
            (int)state,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            isRunning);
}
