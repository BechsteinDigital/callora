using Callora.Core.Application.Plugins;
using Callora.Core.Application.Security;
using Xunit;

namespace Callora.Core.Tests.Application.Security;

/// <summary>
/// Welche Plugin-Berechtigungen in einem Workspace überhaupt etwas bedeuten.
/// </summary>
/// <remarks>
/// Die Filterung nach Aktivierung ist der Grund, warum ein Workspace-Admin diese Rechte bekommen darf.
/// Ohne sie trüge die Sitzung eines Administrators die Rechte jedes Plugins der Installation — auch
/// derer, die sein Workspace nie gesehen hat.
/// </remarks>
public sealed class WorkspacePluginPermissionsTests
{
    [Fact]
    public async Task Nur_die_Plugins_die_in_diesem_Workspace_aktiv_sind()
    {
        var permissions = Permissions(
            active: ["pbx"],
            byPlugin: new()
            {
                ["pbx"] = ["pbx.person.read"],
                ["videoconference"] = ["videoconference.room.update"]
            });

        Assert.Equal(["pbx.person.read"], await permissions.ForWorkspaceAsync("acme"));
    }

    [Fact]
    public async Task Ein_Workspace_ohne_aktive_Plugins_bekommt_nichts()
    {
        var permissions = Permissions(
            active: [], byPlugin: new() { ["pbx"] = ["pbx.person.read"] });

        Assert.Empty(await permissions.ForWorkspaceAsync("acme"));
    }

    [Fact]
    public async Task Ein_aktives_Plugin_ohne_Berechtigungen_ist_kein_Fehler()
    {
        // Der häufige Fall: Ein Plugin, dessen Fläche keine eigenen Rechte braucht. Ein Nachschlagen
        // ohne Treffer darf hier nicht werfen — es hinge an einer Anmeldung.
        var permissions = Permissions(
            active: ["videoconference"], byPlugin: new() { ["pbx"] = ["pbx.person.read"] });

        Assert.Empty(await permissions.ForWorkspaceAsync("acme"));
    }

    [Fact]
    public async Task Ohne_Workspace_gibt_es_nichts_nachzuschlagen()
    {
        // Eine Plattform-Sitzung kommt hier gar nicht vorbei; falls doch, ist „nichts" die Antwort,
        // die keine Rechte erfindet.
        var permissions = Permissions(
            active: ["pbx"], byPlugin: new() { ["pbx"] = ["pbx.person.read"] });

        Assert.Empty(await permissions.ForWorkspaceAsync(null));
        Assert.Empty(await permissions.ForWorkspaceAsync("   "));
    }

    [Fact]
    public async Task Mehrere_aktive_Plugins_kommen_sortiert_und_ohne_Dubletten()
    {
        var permissions = Permissions(
            active: ["pbx", "communication"],
            byPlugin: new()
            {
                ["pbx"] = ["pbx.person.read", "pbx.number.read"],
                ["communication"] = ["communication.calls.read"]
            });

        Assert.Equal(
            ["communication.calls.read", "pbx.number.read", "pbx.person.read"],
            await permissions.ForWorkspaceAsync("acme"));
    }

    private static WorkspacePluginPermissions Permissions(
        IReadOnlyList<string> active, Dictionary<string, IReadOnlyList<string>> byPlugin)
        => new(new StubActivations(active), new StubMap(byPlugin));

    private sealed class StubActivations(IReadOnlyList<string> active) : IWorkspacePluginActivationReader
    {
        public Task<IReadOnlyList<string>> ListActivePluginIdsAsync(
            string workspaceKey, CancellationToken cancellationToken = default)
            => Task.FromResult(active);
    }

    private sealed class StubMap(Dictionary<string, IReadOnlyList<string>> byPlugin) : IPluginPermissionMap
    {
        public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ByPluginAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(byPlugin);
    }
}
