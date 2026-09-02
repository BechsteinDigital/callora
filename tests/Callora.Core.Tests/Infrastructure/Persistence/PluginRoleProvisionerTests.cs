using Callora.Core.Application.Security;
using Callora.Core.Domain.Security;
using Callora.Core.Infrastructure.Persistence;
using Callora.Core.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Persistence;

/// <summary>
/// Die Rolle, die eine Plugin-Installation nach sich zieht.
/// </summary>
/// <remarks>
/// <para>
/// Bis hierher brachte ein Plugin seine Berechtigungsschlüssel mit, und der Betreiber musste sich die
/// Rolle daraus von Hand zusammenklicken. Wer das übersah, hatte ein installiertes Plugin, dessen
/// Oberfläche für jeden außer dem Super-Admin leer blieb — ohne Fehlermeldung, weil aus Sicht der
/// Autorisierung alles stimmte.
/// </para>
/// <para>
/// Der Teil, der hier wirklich geprüft wird, ist das Nicht-Anfassen. Anlegen ist einfach; die
/// Entscheidung eines Betreibers beim nächsten Start nicht stillschweigend zurückzudrehen, ist der
/// Grund, warum es diese Klasse gibt.
/// </para>
/// </remarks>
[Trait("Category", "Slow")]
[Collection(PostgresCollection.Name)]
public sealed class PluginRoleProvisionerTests(PostgresFixture postgres)
{
    private string? _database;

    [SkippableFact]
    public async Task Legt_fuer_ein_Plugin_eine_Rolle_mit_seinen_Berechtigungen_an()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        var options = await FreshDbAsync();

        await using (var db = new HostPersistenceDbContext(options))
        {
            Assert.Equal(1, await Provisioner("pbx", "pbx.person.read", "pbx.number.read")
                .ProvisionAsync(db));
        }

        await using var check = new HostPersistenceDbContext(options);
        var role = await check.BackendRbacRoles
            .Include(candidate => candidate.Permissions)
            .SingleAsync(candidate => candidate.ProvisionedByPluginId == "pbx");

        Assert.Equal("pbx.admin", role.Name);
        Assert.Equal("admin", role.ProvisionedAs);
        Assert.False(role.IsSystem);
        Assert.Equal(
            ["pbx.number.read", "pbx.person.read"],
            role.Permissions.Select(grant => grant.PermissionKey).OrderBy(key => key, StringComparer.Ordinal));
    }

    [SkippableFact]
    public async Task Ein_zweiter_Lauf_legt_nichts_noch_einmal_an()
    {
        // Bei jedem Start. Ohne diese Eigenschaft stünde nach einer Woche für jedes Plugin ein Stapel
        // gleichnamiger Rollen da — beziehungsweise gar keine, weil der Name eindeutig ist und der
        // zweite Versuch am Index scheiterte, mitten im Start.
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        var options = await FreshDbAsync();

        await using (var first = new HostPersistenceDbContext(options))
        {
            await Provisioner("pbx", "pbx.person.read").ProvisionAsync(first);
        }

        await using (var second = new HostPersistenceDbContext(options))
        {
            Assert.Equal(0, await Provisioner("pbx", "pbx.person.read").ProvisionAsync(second));
        }

        await using var check = new HostPersistenceDbContext(options);
        Assert.Equal(1, await check.BackendRbacRoles.CountAsync(role => role.ProvisionedByPluginId == "pbx"));
    }

    [SkippableFact]
    public async Task Was_der_Betreiber_herausgenommen_hat_bleibt_heraussen()
    {
        // Der Kern der Sache. Eine Anpassung, die beim nächsten Start still zurückgedreht wird, ist
        // schlimmer als eine fehlende Berechtigung: Die fehlende sieht man, die zurückgedrehte nicht —
        // und man sieht sie ausgerechnet dann nicht, wenn sie jemandem etwas erlaubt, das er nicht
        // mehr können sollte.
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        var options = await FreshDbAsync();

        await using (var first = new HostPersistenceDbContext(options))
        {
            await Provisioner("pbx", "pbx.person.read", "pbx.person.delete").ProvisionAsync(first);
        }

        await using (var edit = new HostPersistenceDbContext(options))
        {
            var role = await edit.BackendRbacRoles
                .Include(candidate => candidate.Permissions)
                .SingleAsync(candidate => candidate.ProvisionedByPluginId == "pbx");

            role.Permissions.Remove(
                role.Permissions.Single(grant => grant.PermissionKey == "pbx.person.delete"));
            role.Name = "telefonanlage";
            await edit.SaveChangesAsync();
        }

        await using (var again = new HostPersistenceDbContext(options))
        {
            await Provisioner("pbx", "pbx.person.read", "pbx.person.delete").ProvisionAsync(again);
        }

        await using var check = new HostPersistenceDbContext(options);
        var stored = await check.BackendRbacRoles
            .Include(candidate => candidate.Permissions)
            .SingleAsync(candidate => candidate.ProvisionedByPluginId == "pbx");

        // Auch der Name: Die Rolle wird über Plugin und Slug wiedergefunden, nicht über ihren Namen.
        // Hinge die Suche am Namen, stünde jetzt eine zweite daneben und niemand wüsste, welche gilt.
        Assert.Equal("telefonanlage", stored.Name);
        Assert.Equal(["pbx.person.read"], stored.Permissions.Select(grant => grant.PermissionKey));
    }

    [SkippableFact]
    public async Task Eine_gleichnamige_Rolle_eines_Menschen_wird_nicht_uebernommen()
    {
        // Der Rollenname ist global eindeutig. Sie zu übernehmen hieße, ihre Berechtigungen dem
        // Plugin zuzuschlagen — und sie bei der Deinstallation als plugin-eigen zu behandeln, obwohl
        // sie jemandem gehört, der sie sich selbst gegeben hat.
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        var options = await FreshDbAsync();

        await using (var seed = new HostPersistenceDbContext(options))
        {
            seed.BackendRbacRoles.Add(new BackendRbacRole
            {
                Id = Guid.NewGuid(),
                Name = "pbx.admin",
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Permissions = [new BackendRbacRoleGrant { Id = Guid.NewGuid(), PermissionKey = "flow.read" }]
            });
            await seed.SaveChangesAsync();
        }

        await using (var db = new HostPersistenceDbContext(options))
        {
            Assert.Equal(0, await Provisioner("pbx", "pbx.person.read").ProvisionAsync(db));
        }

        await using var check = new HostPersistenceDbContext(options);
        var role = await check.BackendRbacRoles
            .Include(candidate => candidate.Permissions)
            .SingleAsync(candidate => candidate.Name == "pbx.admin");

        Assert.Null(role.ProvisionedByPluginId);
        Assert.Equal(["flow.read"], role.Permissions.Select(grant => grant.PermissionKey));
    }

    [SkippableFact]
    public async Task Rollen_die_ein_Mensch_angelegt_hat_stehen_beliebig_oft_daneben()
    {
        // Der Unique-Index über (ProvisionedByPluginId, ProvisionedAs) steht ihnen nicht im Weg: In
        // Postgres gelten NULL-Werte in einem Unique-Index als verschieden, und beide Spalten sind bei
        // einer von Hand erstellten Rolle NULL.
        //
        // Hier stand einmal, der Filter des Index sei das, was diesen Fall erlaubt. Das ist nachgemessen
        // falsch — ohne ihn läuft dieser Test genauso durch. Der Test bleibt trotzdem: Er hält die
        // Eigenschaft fest, auf die sich der Betreiber verlässt, und würde den Tag bemerken, an dem
        // jemand den Index auf NULLS NOT DISTINCT umstellt.
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        var options = await FreshDbAsync();

        await using var db = new HostPersistenceDbContext(options);
        db.BackendRbacRoles.Add(Named("support"));
        db.BackendRbacRoles.Add(Named("buchhaltung"));

        await db.SaveChangesAsync();

        Assert.Equal(2, await db.BackendRbacRoles.CountAsync());
    }

    private static BackendRbacRole Named(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    private static PluginRoleProvisioner Provisioner(string pluginId, params string[] keys) =>
        new(new StubTemplates(pluginId, keys), NullLogger<PluginRoleProvisioner>.Instance);

    private async Task<string> DatabaseAsync() => _database ??= await postgres.CreateDatabaseAsync();

    private async Task<DbContextOptions<HostPersistenceDbContext>> FreshDbAsync()
    {
        var options = new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(await DatabaseAsync())
            .Options;

        await using var ctx = new HostPersistenceDbContext(options);
        await ctx.Database.EnsureCreatedAsync();
        return options;
    }

    private sealed class StubTemplates(string pluginId, IReadOnlyList<string> keys) : IPluginRoleTemplateSource
    {
        public Task<IReadOnlyList<PluginRoleTemplate>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PluginRoleTemplate>>(
                [new PluginRoleTemplate(pluginId, "admin", $"{pluginId}.admin", keys)]);
    }
}
