using Callora.Core.Domain.Security;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Persistence;
using Callora.Core.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Persistence;

/// <summary>
/// Rollen je Mitgliedschaft — beliebig viele, und beim Entziehen wirklich weg.
/// </summary>
/// <remarks>
/// Der Teil, der hier zählt, ist das Ersetzen: Eine Oberfläche zeigt Kästchen, und der Zustand danach
/// ist das, was der Betreiber gesehen hat. Eine Zuweisung, die ein Ersetzen überlebt, ist eine
/// Berechtigung, von der er glaubt, sie sei weg.
/// </remarks>
[Trait("Category", "Slow")]
[Collection(PostgresCollection.Name)]
public sealed class WorkspaceMembershipRoleStoreTests(PostgresFixture postgres)
{
    private const string WorkspaceKey = "acme";

    private string? _database;

    [SkippableFact]
    public async Task Eine_Mitgliedschaft_kann_mehrere_Rollen_tragen()
    {
        // Die Anforderung selbst: „PBX lesen" und „Medien verwalten" sind zwei Entscheidungen.
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        var options = await FreshDbAsync();
        await GivenMembershipAsync(options, "alice", roles: ["pbx.viewer", "medien"]);

        await using var db = new HostPersistenceDbContext(options);
        var store = new EfWorkspaceMembershipRoleStore(db);

        var stored = await store.ReplaceRolesAsync(WorkspaceKey, "alice", ["pbx.viewer", "medien"]);

        Assert.Equal(["medien", "pbx.viewer"], stored);
        Assert.Equal(["medien", "pbx.viewer"], await store.ListRolesAsync(WorkspaceKey, "alice"));
    }

    [SkippableFact]
    public async Task Ersetzen_nimmt_weg_was_nicht_mehr_dabei_ist()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        var options = await FreshDbAsync();
        await GivenMembershipAsync(options, "alice", roles: ["pbx.viewer", "medien"]);

        await using var db = new HostPersistenceDbContext(options);
        var store = new EfWorkspaceMembershipRoleStore(db);
        await store.ReplaceRolesAsync(WorkspaceKey, "alice", ["pbx.viewer", "medien"]);

        await store.ReplaceRolesAsync(WorkspaceKey, "alice", ["medien"]);

        Assert.Equal(["medien"], await store.ListRolesAsync(WorkspaceKey, "alice"));
    }

    [SkippableFact]
    public async Task Eine_leere_Liste_nimmt_alle_weg()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        var options = await FreshDbAsync();
        await GivenMembershipAsync(options, "alice", roles: ["pbx.viewer"]);

        await using var db = new HostPersistenceDbContext(options);
        var store = new EfWorkspaceMembershipRoleStore(db);
        await store.ReplaceRolesAsync(WorkspaceKey, "alice", ["pbx.viewer"]);

        Assert.Empty((await store.ReplaceRolesAsync(WorkspaceKey, "alice", []))!);
        Assert.Empty(await store.ListRolesAsync(WorkspaceKey, "alice"));
    }

    [SkippableFact]
    public async Task Eine_Rolle_die_es_nicht_gibt_wird_nicht_zugewiesen()
    {
        // Sie wäre eine Zeile, die nichts bewirkt und in der Oberfläche aussieht, als bewirke sie
        // etwas — und der Betreiber hätte keinen Anlass, sie zu prüfen.
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        var options = await FreshDbAsync();
        await GivenMembershipAsync(options, "alice", roles: ["pbx.viewer"]);

        await using var db = new HostPersistenceDbContext(options);
        var store = new EfWorkspaceMembershipRoleStore(db);

        var stored = await store.ReplaceRolesAsync(WorkspaceKey, "alice", ["pbx.viewer", "gibt-es-nicht"]);

        Assert.Equal(["pbx.viewer"], stored);
    }

    [SkippableFact]
    public async Task Wer_kein_Mitglied_ist_bekommt_keine_Rollen()
    {
        // Nicht „angelegt und wirkungslos": Eine Zuweisung ohne Mitgliedschaft hätte keinen Workspace,
        // in dem sie gälte, und stünde trotzdem in der Tabelle.
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        var options = await FreshDbAsync();
        await GivenMembershipAsync(options, "alice", roles: ["pbx.viewer"]);

        await using var db = new HostPersistenceDbContext(options);
        var store = new EfWorkspaceMembershipRoleStore(db);

        Assert.Null(await store.ReplaceRolesAsync(WorkspaceKey, "bob", ["pbx.viewer"]));
    }

    [SkippableFact]
    public async Task Das_Loeschen_einer_Rolle_nimmt_ihre_Zuweisungen_mit()
    {
        // Kaskade, und das ist eine Entscheidung: Restrict machte aus einem gewollten Löschen einen
        // 500er, den niemand lesen kann. Die Zuweisung ohne ihre Rolle bedeutet ohnehin nichts.
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        var options = await FreshDbAsync();
        await GivenMembershipAsync(options, "alice", roles: ["pbx.viewer"]);

        await using (var assign = new HostPersistenceDbContext(options))
        {
            await new EfWorkspaceMembershipRoleStore(assign)
                .ReplaceRolesAsync(WorkspaceKey, "alice", ["pbx.viewer"]);
        }

        await using (var delete = new HostPersistenceDbContext(options))
        {
            delete.BackendRbacRoles.Remove(
                await delete.BackendRbacRoles.SingleAsync(role => role.Name == "pbx.viewer"));
            await delete.SaveChangesAsync();
        }

        await using var check = new HostPersistenceDbContext(options);
        Assert.Empty(await new EfWorkspaceMembershipRoleStore(check).ListRolesAsync(WorkspaceKey, "alice"));
    }

    [SkippableFact]
    public async Task Wer_eine_Rolle_traegt_ist_fuer_den_Widerruf_auffindbar()
    {
        // Berechtigungen stehen im Token. Ohne diese Abfrage behielte jemand, dessen Rolle gerade
        // geändert wurde, die alten Rechte bis zum Ablauf seiner Sitzung.
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");
        var options = await FreshDbAsync();
        await GivenMembershipAsync(options, "alice", roles: ["pbx.viewer"]);

        await using var db = new HostPersistenceDbContext(options);
        var store = new EfWorkspaceMembershipRoleStore(db);
        await store.ReplaceRolesAsync(WorkspaceKey, "alice", ["pbx.viewer"]);

        Assert.Equal(["alice"], await store.ListUsersWithRoleAsync("pbx.viewer"));
    }

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

    private static async Task GivenMembershipAsync(
        DbContextOptions<HostPersistenceDbContext> options, string externalId, IReadOnlyList<string> roles)
    {
        await using var db = new HostPersistenceDbContext(options);

        var tenant = new Callora.Core.Domain.Tenants.Tenant
        {
            Id = Guid.NewGuid(),
            TenantKey = "default",
            DisplayName = "Default",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        db.Tenants.Add(tenant);

        var workspace = new Callora.Core.Domain.Workspaces.Workspace
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            WorkspaceKey = WorkspaceKey,
            DisplayName = "Acme",
            WorkspaceType = "internal",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        db.Workspaces.Add(workspace);

        var user = new BackendUser
        {
            Id = Guid.NewGuid(),
            ExternalId = externalId,
            SecurityStamp = Guid.NewGuid().ToString("n"),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        db.BackendUsers.Add(user);

        db.WorkspaceMemberships.Add(new WorkspaceMembership
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            UserId = user.Id,
            Role = "member",
            AssignedAtUtc = DateTimeOffset.UtcNow
        });

        foreach (var role in roles)
        {
            db.BackendRbacRoles.Add(new BackendRbacRole
            {
                Id = Guid.NewGuid(),
                Name = role,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }
}
