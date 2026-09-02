using Callora.Core.Application.Security;
using Xunit;

namespace Callora.Core.Tests.Application.Security;

/// <summary>
/// Welche Rolle eine Installation nach sich zieht.
/// </summary>
/// <remarks>
/// Woher die Schlüssel kommen, prüft <see cref="PluginPermissionMapTests"/>. Hier geht es nur noch um
/// den Zuschnitt: eine Rolle je Plugin, benannt wie die Rollen des Kerns, und keine für ein Plugin,
/// das nichts zu vergeben hat.
/// </remarks>
public sealed class PluginAdminRoleTemplatesTests
{
    [Fact]
    public async Task Ein_Plugin_mit_Berechtigungen_bekommt_eine_Rolle()
    {
        var templates = Templates(new() { ["pbx"] = ["pbx.person.read", "pbx.person.create"] });

        var role = Assert.Single(await templates.ListAsync());

        Assert.Equal("pbx", role.PluginId);
        Assert.Equal("admin", role.Slug);
        // Dieselbe Form wie "superadmin" und "host.api": ein Bezeichner, kein Fließtext. Ein
        // sprechender Name müsste übersetzt werden, und ein Rollenname steht in Tokens, Logzeilen und
        // Skripten.
        Assert.Equal("pbx.admin", role.RoleName);
        Assert.Equal(["pbx.person.read", "pbx.person.create"], role.PermissionKeys);
    }

    [Fact]
    public async Task Ein_Plugin_ohne_Berechtigungen_bekommt_keine()
    {
        // Eine Rolle ohne Berechtigungen ist eine Zeile, die nichts kann und trotzdem vergeben werden
        // will — sie kostet den Betreiber genau die Zeit, die dieser Weg sparen soll.
        var templates = Templates([]);

        Assert.Empty(await templates.ListAsync());
    }

    [Fact]
    public async Task Die_Rollen_kommen_in_stabiler_Reihenfolge()
    {
        // Damit zwei Läufe dieselbe Antwort geben. Eine Reihenfolge aus einer Hashtabelle wäre die Art
        // Unterschied, die man erst in einem Diff bemerkt, das niemand erwartet hat.
        var templates = Templates(new()
        {
            ["pbx"] = ["pbx.person.read"],
            ["communication"] = ["communication.calls.read"]
        });

        Assert.Equal(["communication", "pbx"], (await templates.ListAsync()).Select(role => role.PluginId));
    }

    private static PluginAdminRoleTemplates Templates(Dictionary<string, IReadOnlyList<string>> byPlugin)
        => new(new StubMap(byPlugin));

    private sealed class StubMap(Dictionary<string, IReadOnlyList<string>> byPlugin) : IPluginPermissionMap
    {
        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ByPluginAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(byPlugin);
    }
}
