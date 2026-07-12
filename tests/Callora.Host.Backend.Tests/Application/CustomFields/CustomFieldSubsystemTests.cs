using Callora.Host.Backend.Application.Plugins;
using Callora.Host.Backend.Infrastructure.CustomFields;
using Callora.Host.PluginContracts.Application.Migrations;
using Xunit;

namespace Callora.Host.Backend.Tests.Application.CustomFields;

public sealed class CustomFieldSubsystemTests
{
    [Fact]
    public void RegistryParser_ReadsEntitiesAndFields_IgnoresUnknownEntities()
    {
        const string registryJson = """
        {
          "pluginId": "crm",
          "customFields": {
            "workspace": {
              "crm.accountId": { "label": "CRM Account", "type": "text" },
              "crm.tier": { "label": "Kundenstufe", "type": "select", "order": 5 }
            },
            "call": {
              "crm.dealId": { "label": "Deal" }
            },
            "invoice": {
              "ignored.field": { "label": "Ignored" }
            }
          }
        }
        """;

        var definitions = RegistryCustomFieldSyncService.ParseCustomFields("crm", "1.0.0", registryJson);

        Assert.Equal(3, definitions.Count);
        Assert.Equal(2, definitions.Count(d => d.EntityName == "workspace"));
        Assert.Single(definitions, d => d.EntityName == "call");
        Assert.DoesNotContain(definitions, d => d.EntityName == "invoice");
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
