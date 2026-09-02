using System.Text.RegularExpressions;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.DependencyInjection;

/// <summary>
/// Die Rollenanlage läuft nach dem Laden der Plugins, nicht davor.
/// </summary>
/// <remarks>
/// <para>
/// Ein Plugin liefert seine Berechtigungen auf zwei Wegen: im Manifest oder über einen
/// <c>IHostAdminApiExtensionContributor</c>. Der zweite existiert erst, wenn das Plugin geladen ist —
/// und von den heute installierten Plugins gehen die meisten diesen Weg.
/// </para>
/// <para>
/// Stünde die Registrierung weiter oben, bekämen genau diese Plugins keine Rolle. Und zwar wortlos: Eine
/// leere Schlüsselliste ist von „hat keine Berechtigungen" nicht zu unterscheiden, der Start liefe durch,
/// und der Betreiber fände es an einer Oberfläche, die für alle außer dem Super-Admin leer bleibt.
/// </para>
/// <para>
/// Geprüft wird die Quelle, nicht ein aufgebauter Container: Die Reihenfolge, um die es geht, ist die
/// Reihenfolge der <c>AddHostedService</c>-Aufrufe in dieser Datei, und ein Container gäbe sie nur
/// wieder. Eine Zeile zu verschieben ist die Bewegung, die dieser Test bemerken soll.
/// </para>
/// </remarks>
public sealed class PluginRoleProvisioningIsWiredLastTests
{
    private static readonly string Composition = File.ReadAllText(
        SourceFile("src/Core/Infrastructure/DependencyInjection/CalloraHostCompositionExtensions.cs"));

    [Fact]
    public void Die_Rollenanlage_steht_nach_der_Plugin_Rehydrierung()
    {
        var rehydration = IndexOfHostedService("PluginRuntimeRehydrationHostedService");
        var provisioning = IndexOfHostedService("PluginRoleProvisioningHostedService");

        Assert.True(
            provisioning > rehydration,
            "PluginRoleProvisioningHostedService muss NACH PluginRuntimeRehydrationHostedService "
            + "registriert sein — sonst sind die Plugins noch nicht geladen und die Rollen der "
            + "Plugins, die ihre Schlüssel über einen Contributor liefern, entstehen nie.");
    }

    [Fact]
    public void Der_Ereignis_Weg_ist_ebenfalls_verdrahtet()
    {
        // Der Start-Durchgang fängt, was schon installiert ist; der Abonnent fängt, was danach kommt.
        // Ohne ihn bekäme ein frisch installiertes Plugin seine Rolle erst beim nächsten Neustart.
        Assert.Contains("PluginRoleSyncSubscriber>()", Composition, StringComparison.Ordinal);
    }

    private static int IndexOfHostedService(string typeName)
    {
        var match = Regex.Match(
            Composition,
            $@"AddHostedService<{Regex.Escape(typeName)}>\(\)",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        Assert.True(
            match.Success,
            $"{typeName} ist in der Komposition nicht mehr registriert; dieser Test prüft nichts mehr.");

        return match.Index;
    }

    /// <summary>
    /// Eine Datei relativ zur Repository-Wurzel, gefunden durch Hochlaufen vom Testbinär.
    /// </summary>
    /// <remarks>
    /// Gelaufen statt geraten: Wie tief bin/&lt;config&gt;/&lt;tfm&gt; unter der Wurzel liegt, ist eine
    /// Eigenschaft des Builds — fest verdrahtet scheitert der Test an dem Tag, an dem jemand ein
    /// Zielframework ändert, mit einem Pfad in der Meldung und nichts über den Grund.
    /// </remarks>
    private static string SourceFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, relativePath)))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, relativePath);
    }
}
