using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Security;
using Callora.Core.Tests.Support;
using Xunit;

namespace Callora.Core.Tests.Application.Security;

/// <summary>
/// Declaring a key is only half a fix. Until the key appears in the inventory an operator
/// browses, the plugin is still installed and still answering 403 — the declaration just
/// moved the dead end into the manifest.
/// </summary>
/// <remarks>
/// <para>
/// The inventory already carried plugin keys from <c>IHostAdminApiExtensionContributor</c>,
/// so a plugin contributing Admin-API routes could always supply its own. A plugin whose
/// surface is <c>IApiController</c> routes had no such path — the gap was narrower than
/// #345 described, and in exactly the place that hurts a marketplace plugin.
/// </para>
/// <para>
/// The same class of bug is on record twice in this area: see the remark on
/// <see cref="BackendPermissionKeyValidator"/> — "Absicherung wirksam, Vergabe unmöglich."
/// </para>
/// </remarks>
public sealed class DeclaredKeysBecomeGrantableTests
{
    [Fact]
    public void A_manifest_declared_key_reaches_the_inventory()
    {
        var inventory = BackendPermissionInventory.All(
            new StaticPluginCatalog([]),
            declaredByManifest: ["communication.trunk.update"]);

        Assert.Contains("communication.trunk.update", inventory, StringComparer.Ordinal);
    }

    [Fact]
    public void Host_keys_are_still_there()
    {
        var inventory = BackendPermissionInventory.All(
            new StaticPluginCatalog([]),
            declaredByManifest: ["communication.trunk.update"]);

        Assert.Contains(BackendPermissionKeys.RoleRead, inventory, StringComparer.Ordinal);
    }

    [Fact]
    public void Contributor_keys_and_manifest_keys_both_arrive()
    {
        // Two supply paths, one inventory. A plugin may use either; an operator should not
        // have to know which.
        var catalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IHostAdminApiExtensionContributor)] = [new ContributingStub("composer.layout.publish")]
        });

        var inventory = BackendPermissionInventory.All(
            catalog,
            declaredByManifest: ["communication.trunk.update"]);

        Assert.Contains("composer.layout.publish", inventory, StringComparer.Ordinal);
        Assert.Contains("communication.trunk.update", inventory, StringComparer.Ordinal);
    }

    [Fact]
    public void A_structurally_invalid_declared_key_is_dropped_rather_than_offered()
    {
        // Manifest reading already refuses these, so this is depth rather than duplication:
        // the inventory is also fed by older installs whose manifest predates that check,
        // and offering a key that can never match would put the operator back where they
        // started.
        var inventory = BackendPermissionInventory.All(
            new StaticPluginCatalog([]),
            declaredByManifest: ["nonsense", "communication.trunk.update"]);

        Assert.DoesNotContain("nonsense", inventory, StringComparer.Ordinal);
        Assert.Contains("communication.trunk.update", inventory, StringComparer.Ordinal);
    }

    [Fact]
    public void The_inventory_is_unchanged_when_nothing_is_declared()
    {
        var withNothing = BackendPermissionInventory.All(new StaticPluginCatalog([]));
        var withEmpty = BackendPermissionInventory.All(new StaticPluginCatalog([]), declaredByManifest: []);

        Assert.Equal(withNothing, withEmpty);
    }
}

internal sealed class ContributingStub(string permissionKey) : IHostAdminApiExtensionContributor
{
    public string PluginId => "composer";

    public IReadOnlyList<string> PermissionKeys { get; } = [permissionKey];

    public IReadOnlyList<HostAdminApiRouteRegistration> Routes => [];

    public IReadOnlyList<HostAdminNavigationItem> NavigationItems => [];
}
