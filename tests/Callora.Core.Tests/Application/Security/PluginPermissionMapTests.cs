using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Security;
using Xunit;

namespace Callora.Core.Tests.Application.Security;

/// <summary>
/// Wem welche Berechtigung gehört.
/// </summary>
/// <remarks>
/// <para>
/// Zwei Verwender hängen daran: die Rolle, die eine Installation anlegt, und die Sitzung eines
/// Workspace-Admins. Der zweite macht aus dieser Zuordnung eine Sicherheitsgrenze — was hier einem
/// Plugin zugeschlagen wird, landet in einem Token.
/// </para>
/// <para>
/// Der Punkt, an dem es schiefgeht, ist die Quelle: Ein Plugin liefert seine Schlüssel entweder im
/// Manifest oder über einen Contributor, und von den heute installierten Plugins nutzt genau eines den
/// ersten Weg. Nur eine Quelle zu lesen hieße, die anderen zu übergehen — wortlos, weil eine leere
/// Schlüsselliste sich von „hat keine Berechtigungen" nicht unterscheiden lässt.
/// </para>
/// </remarks>
public sealed class PluginPermissionMapTests
{
    [Fact]
    public async Task Was_im_Manifest_deklariert_ist_zaehlt()
    {
        var map = Map(declared: new() { ["pbx"] = ["pbx.person.read", "pbx.person.create"] });

        var byPlugin = await map.ByPluginAsync();

        Assert.Equal(["pbx.person.create", "pbx.person.read"], byPlugin["pbx"]);
    }

    [Fact]
    public async Task Was_ein_Contributor_beisteuert_ebenso()
    {
        // Der Weg, den die meisten installierten Plugins gehen. Er ist der ältere und der leichter zu
        // übersehende, weil er nicht in der registry.json steht.
        var map = Map(contributors: [new StubContributor("composer", ["composer.layout.read"])]);

        Assert.Equal(["composer.layout.read"], (await map.ByPluginAsync())["composer"]);
    }

    [Fact]
    public async Task Beide_Wege_eines_Plugins_landen_zusammen()
    {
        var map = Map(
            declared: new() { ["pbx"] = ["pbx.person.read"] },
            contributors: [new StubContributor("pbx", ["pbx.number.read"])]);

        Assert.Equal(["pbx.number.read", "pbx.person.read"], (await map.ByPluginAsync())["pbx"]);
    }

    [Fact]
    public async Task Ein_Schluessel_ausserhalb_des_eigenen_Namensraums_wird_verworfen()
    {
        // Die Grenze, die den Rest überhaupt vertretbar macht. Ohne sie könnte ein Plugin über seine
        // beigesteuerten Schlüssel "user.delete" in die Sitzung eines Workspace-Admins schreiben — eine
        // Berechtigung, die über den Workspace hinausreicht, aus einem Plugin heraus, das in ihm aktiv
        // ist. Das Manifest weist so etwas seit jeher ab; der Contributor-Weg hatte diese Grenze nie.
        var map = Map(contributors:
            [new StubContributor("pbx", ["pbx.person.read", "user.delete", "communication.calls.read"])]);

        Assert.Equal(["pbx.person.read"], (await map.ByPluginAsync())["pbx"]);
    }

    [Fact]
    public async Task Eine_Aktion_die_der_Kern_nicht_kennt_bleibt_erhalten()
    {
        // Bewusst NICHT die Aktionsliste des Manifests. composer.layout.publish und
        // communication.accounts.manage sind seit jeher in Betrieb; sie hier wegzufiltern hieße, zwei
        // laufenden Plugins ihre Berechtigungen zu nehmen, um eine Stilregel durchzusetzen, die für
        // ihren Weg nie galt.
        var map = Map(contributors:
            [new StubContributor("composer", ["composer.layout.publish"])]);

        Assert.Equal(["composer.layout.publish"], (await map.ByPluginAsync())["composer"]);
    }

    [Fact]
    public async Task Ein_strukturell_kaputter_Schluessel_wird_verworfen()
    {
        // Dieselbe Prüfung wie im Inventar: Ein Schlüssel ohne Punkt greift zur Laufzeit nie, und was
        // das Inventar anbietet, soll hier nicht verschwiegen werden — und umgekehrt.
        var map = Map(contributors: [new StubContributor("pbx", ["pbx.person.read", "pbx", "", ".read"])]);

        Assert.Equal(["pbx.person.read"], (await map.ByPluginAsync())["pbx"]);
    }

    [Fact]
    public async Task Ein_Plugin_ohne_verwertbare_Schluessel_steht_nicht_in_der_Zuordnung()
    {
        // Sonst stünde es als Eintrag mit leerer Liste da, und jeder Verwender müsste den Unterschied
        // zwischen „kein Eintrag" und „leerer Eintrag" selbst kennen.
        var map = Map(contributors: [new StubContributor("videoconference", ["user.delete"])]);

        Assert.Empty(await map.ByPluginAsync());
    }

    private static PluginPermissionMap Map(
        Dictionary<string, IReadOnlyList<string>>? declared = null,
        IReadOnlyList<IHostAdminApiExtensionContributor>? contributors = null)
        => new(new StubCatalog(contributors ?? []), new StubDeclaredPermissions(declared ?? []));

    internal sealed class StubContributor(string pluginId, IReadOnlyList<string> keys)
        : IHostAdminApiExtensionContributor
    {
        public string PluginId => pluginId;

        public IReadOnlyList<string> PermissionKeys => keys;

        public IReadOnlyList<HostAdminApiRouteRegistration> Routes => [];

        public IReadOnlyList<HostAdminNavigationItem> NavigationItems => [];
    }

    internal sealed class StubCatalog(IReadOnlyList<IHostAdminApiExtensionContributor> contributors)
        : ICalloraPluginCatalog
    {
        public bool TryGetExport(Type contractType, out object? service)
        {
            var exports = GetExports(contractType);
            service = exports.Count > 0 ? exports[0] : null;
            return service is not null;
        }

        public IReadOnlyList<object> GetExports(Type contractType)
            => contractType == typeof(IHostAdminApiExtensionContributor) ? [.. contributors] : [];

        public IReadOnlyList<CalloraPluginExport> GetOwnedExports(Type contractType) => [];
    }

    internal sealed class StubDeclaredPermissions(Dictionary<string, IReadOnlyList<string>> byPlugin)
        : IPluginDeclaredPermissionCatalog
    {
        public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(
                [.. byPlugin.Values.SelectMany(keys => keys).Distinct(StringComparer.Ordinal)]);

        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ListByPluginAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(byPlugin);
    }
}
