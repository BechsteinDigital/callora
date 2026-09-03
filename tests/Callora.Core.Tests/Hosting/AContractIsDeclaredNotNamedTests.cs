using Callora.Core.Application.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Callora.Core.Tests.Hosting;

/// <summary>
/// Ob ein Plugin einen Typ auflösen darf, entscheidet die Deklaration — nicht der Name der Assembly.
/// </summary>
/// <remarks>
/// <para>
/// Die kuratierte Dienstoberfläche ließ Assemblies durch, die mit <c>Callora.Plugin.</c> beginnen und
/// auf <c>.Abstractions</c> enden. Das trug, solange alle Plugins aus einem Haus kamen. Ein
/// Fremdanbieter, der sein Vertragspaket <c>Acme.Crm.Contracts</c> nennt, fiel durch — sein Plugin
/// installiert, lädt und exportiert, und kein zweites Plugin kann den Vertrag auflösen. Die Ablehnung
/// hätte nichts mit Richtigkeit zu tun, sondern mit einer Konvention, die er nicht kennen konnte.
/// </para>
/// <para>
/// Gefragt wird jetzt das Manifest: Was ein Plugin unter <c>contracts</c> deklariert, hebt der Host
/// ohnehin in den geteilten Ladekontext. Die Auskunft lag vor; dieses Tor hat sie nur nicht benutzt.
/// Damit gilt ADR-025 auch an der vierten Stelle — kein Mechanismus leitet aus dem Namen ab, wer eine
/// Assembly stellt.
/// </para>
/// </remarks>
public sealed class AContractIsDeclaredNotNamedTests
{
    [Fact]
    public void Ein_deklarierter_Vertrag_wird_aufgeloest_egal_wie_er_heisst()
    {
        // Der Fall des Fremdanbieters. Der Typ stammt hier aus der Testassembly, deren Name auf keine
        // der alten Regeln passt — genau das ist der Punkt.
        var services = new ServiceCollection()
            .AddSingleton(new AcmeContract())
            .BuildServiceProvider();

        // Über die echte API deklariert, nicht über einen Testhaken: Die Assembly liegt bereits im
        // Default-Kontext, also vermerkt die Registry sie als host-provided, ohne sie zu laden — genau
        // der Weg, den ein Vertrag geht, den der Host selbst mitbringt.
        var declared = new SharedContractAssemblyRegistry();
        declared.RegisterContracts(
            AppContext.BaseDirectory,
            [$"{typeof(AcmeContract).Assembly.GetName().Name}.dll"],
            "acme.crm");

        var curated = new CuratedPluginServiceProvider(services, "acme.crm", sharedContracts: declared);

        Assert.NotNull(curated.GetService(typeof(AcmeContract)));
    }

    [Fact]
    public void Was_niemand_deklariert_hat_bleibt_draussen()
    {
        // Die Gegenprobe, und der eigentliche Zweck des Tors: Ein Plugin sieht veröffentlichte
        // Verträge, nicht den Wurzel-Container des Hosts. Ohne diese Grenze wäre die kuratierte
        // Oberfläche keine.
        var services = new ServiceCollection()
            .AddSingleton(new AcmeContract())
            .BuildServiceProvider();

        var curated = new CuratedPluginServiceProvider(
            services, "acme.crm", sharedContracts: new SharedContractAssemblyRegistry());

        Assert.Null(curated.GetService(typeof(AcmeContract)));
    }

    [Fact]
    public void Ohne_Registry_bleibt_es_beim_Kern_und_nichts_sonst()
    {
        // Ein von Hand zusammengesetzter Aufbau ohne Registry verhält sich wie vorher für alles, was
        // der Kern selbst veröffentlicht — und lässt nichts durch, was ein Plugin deklariert hätte.
        var services = new ServiceCollection()
            .AddSingleton(new AcmeContract())
            .BuildServiceProvider();

        var curated = new CuratedPluginServiceProvider(services, "acme.crm");

        Assert.Null(curated.GetService(typeof(AcmeContract)));
    }

    /// <summary>Ein Vertragstyp, dessen Assembly auf keine der früheren Namensregeln passt.</summary>
    private sealed class AcmeContract;
}
