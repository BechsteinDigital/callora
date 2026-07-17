using Callora.Core.Application.Migrations.Contracts;
using Callora.Core.Application.Plugins;
using Callora.Core.Infrastructure.CustomFields;
using Xunit;

namespace Callora.Core.Tests.Application.CustomFields;

public sealed class CustomFieldSubsystemTests
{
    [Fact]
    public void RegistryParser_ReadsAllDeclaredEntities_SkipsMalformed()
    {
        // Domain-neutral: no entity whitelist. Every well-formed entity a plugin
        // declares is read (core "workspace" and a plugin-defined "contact" alike);
        // only structurally malformed declarations are skipped.
        const string registryJson = """
        {
          "pluginId": "crm",
          "customFields": {
            "workspace": {
              "crm.accountId": { "label": "CRM Account", "type": "text" },
              "crm.tier": { "label": "Kundenstufe", "type": "select", "order": 5 }
            },
            "contact": {
              "crm.dealId": { "label": "Deal" }
            },
            "broken": "not-an-object"
          }
        }
        """;

        var definitions = RegistryCustomFieldSyncService.ParseCustomFields("crm", "1.0.0", registryJson);

        Assert.Equal(3, definitions.Count);
        Assert.Equal(2, definitions.Count(d => d.EntityName == "workspace"));
        Assert.Single(definitions, d => d.EntityName == "contact");
        Assert.DoesNotContain(definitions, d => d.EntityName == "broken");
        Assert.Equal(5, definitions.Single(d => d.FieldKey == "crm.tier").SortOrder);
    }

    [Fact]
    public void MigrationPlanner_SelectsPendingInVersionOrder()
    {
        var pending = PluginMigrationPlanner.SelectPending(
            appliedVersions: [1],
            migrations: [new TestMigration(3), new TestMigration(1), new TestMigration(2)]);

        Assert.Equal([2, 3], pending.Select(m => m.Version));
    }

    [Fact]
    public void MigrationPlanner_RejectsDuplicateVersions()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PluginMigrationPlanner.SelectPending([], [new TestMigration(1), new TestMigration(1)]));
    }

    private sealed class TestMigration(int version) : IPluginMigration
    {
        public int Version => version;

        public string Description => $"test migration {version}";

        public Task UpAsync(
            System.Data.Common.DbConnection connection,
            System.Data.Common.DbTransaction transaction,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
