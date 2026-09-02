using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Security;
using Xunit;

namespace Callora.Core.Tests.Application.Security;

/// <summary>
/// Welche Rollen eine Installation nach sich zieht.
/// </summary>
/// <remarks>
/// Der Punkt, an dem das schiefgeht, ist nicht das Anlegen, sondern die Quelle: Ein Plugin liefert
/// seine Berechtigungen entweder im Manifest oder über einen Contributor, und von den heute
/// installierten Plugins nutzt genau eines den ersten Weg. Nur eine Quelle zu lesen hieße, für die
/// übrigen keine Rolle anzulegen — wortlos, weil eine leere Schlüsselliste sich nicht von „hat keine
/// Berechtigungen" unterscheiden lässt.
/// </remarks>
public sealed class PluginAdminRoleTemplatesTests
{
    [Fact]
    public async Task Ein_Plugin_das_im_Manifest_deklariert_bekommt_eine_Rolle()
    {
        var templates = Templates(
            declared: new() { ["pbx"] = ["pbx.person.read", "pbx.person.create"] });

        var role = Assert.Single(await templates.ListAsync());

        Assert.Equal("pbx", role.PluginId);
        Assert.Equal("pbx.admin", role.RoleName);
        Assert.Equal(["pbx.person.create", "pbx.person.read"], role.PermissionKeys);
    }

    [Fact]
    public async Task Ein_Plugin_das_ueber_den_Contributor_liefert_ebenso()
    {
        // Der Weg, den drei von vier installierten Plugins gehen. Er ist der ältere und in der
        // Rollenanlage der leichter zu übersehende, weil er nicht in der registry.json steht.
        var templates = Templates(
            contributors: [new StubContributor("composer", ["composer.package.read"])]);

        var role = Assert.Single(await templates.ListAsync());

        Assert.Equal("composer.admin", role.RoleName);
    }

    [Fact]
    public async Task Beide_Wege_eines_Plugins_landen_in_einer_Rolle()
    {
        // Nicht in zweien. Welchen Weg ein Plugin benutzt, ist seine Bauentscheidung; für den
        // Betreiber ist es ein Plugin und gehört eine Rolle.
        var templates = Templates(
            declared: new() { ["pbx"] = ["pbx.person.read"] },
            contributors: [new StubContributor("pbx", ["pbx.number.read"])]);

        var role = Assert.Single(await templates.ListAsync());

        Assert.Equal(["pbx.number.read", "pbx.person.read"], role.PermissionKeys);
    }

    [Fact]
    public async Task Ein_Plugin_ohne_Berechtigungen_bekommt_keine_Rolle()
    {
        // Eine Rolle ohne Berechtigungen ist eine Zeile, die nichts kann und trotzdem vergeben
        // werden will — sie kostet den Betreiber genau die Zeit, die dieser Weg sparen soll.
        var templates = Templates(contributors: [new StubContributor("videoconference", [])]);

        Assert.Empty(await templates.ListAsync());
    }

    [Fact]
    public async Task Ein_strukturell_kaputter_Schluessel_kommt_nicht_in_die_Rolle()
    {
        // Dieselbe Prüfung wie im Inventar — nicht mehr und nicht weniger. Ein Schlüssel, den der
        // Betreiber im Rollen-Endpunkt angeboten bekommt, den diese Rolle aber verschweigt, wäre ein
        // Unterschied, den ihm niemand erklären kann.
        //
        // Der Prüfer sieht die STRUKTUR an, nicht das Vokabular, und ausdrücklich auch nicht die
        // Groß-/Kleinschreibung — die Begründung dazu steht in BackendPermissionKey.TryParse. Ein
        // "PBX.Person.Read" käme also durch und griffe zur Laufzeit nie, weil dort ordinal verglichen
        // wird. Das Manifest weist es ab (PluginPermissionKeyPolicy verlangt Kleinschreibung), der
        // Contributor-Weg nicht. Diese Lücke gehört dorthin geschlossen, nicht hier heimlich
        // strenger gemacht: Zwei Prüfungen, die verschieden streng sind, sind der Ursprung genau
        // solcher Fälle.
        var templates = Templates(
            contributors: [new StubContributor("pbx", ["pbx.person.read", "pbx", "", ".read"])]);

        var role = Assert.Single(await templates.ListAsync());

        Assert.Equal(["pbx.person.read"], role.PermissionKeys);
    }

    [Fact]
    public async Task Die_Rollen_kommen_in_stabiler_Reihenfolge()
    {
        // Damit zwei Läufe dieselbe Antwort geben. Eine Reihenfolge aus einer Hashtabelle wäre die
        // Art Unterschied, die man erst in einem Diff bemerkt, das niemand erwartet hat.
        var templates = Templates(
            declared: new() { ["pbx"] = ["pbx.person.read"] },
            contributors: [new StubContributor("communication", ["communication.trunk.read"])]);

        var roles = await templates.ListAsync();

        Assert.Equal(["communication", "pbx"], roles.Select(role => role.PluginId));
    }

    private static PluginAdminRoleTemplates Templates(
        Dictionary<string, IReadOnlyList<string>>? declared = null,
        IReadOnlyList<IHostAdminApiExtensionContributor>? contributors = null)
        => new(new StubCatalog(contributors ?? []), new StubDeclaredPermissions(declared ?? []));

    private sealed class StubContributor(string pluginId, IReadOnlyList<string> keys)
        : IHostAdminApiExtensionContributor
    {
        public string PluginId => pluginId;

        public IReadOnlyList<string> PermissionKeys => keys;

        public IReadOnlyList<HostAdminApiRouteRegistration> Routes => [];

        public IReadOnlyList<HostAdminNavigationItem> NavigationItems => [];
    }

    private sealed class StubCatalog(IReadOnlyList<IHostAdminApiExtensionContributor> contributors)
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

    private sealed class StubDeclaredPermissions(Dictionary<string, IReadOnlyList<string>> byPlugin)
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
