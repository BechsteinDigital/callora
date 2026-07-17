using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Application.Policies;
using Callora.Core.Tests.Support;
using Xunit;

namespace Callora.Core.Tests.Application.Entitlements;

public sealed class MarketplaceEntitlementApplierTests
{
    [Fact]
    public async Task Grant_ActivatesEntitlement()
    {
        var (applier, entitlements) = CreateApplier();

        var applied = await applier.ApplyAsync(NewEvent("evt-1", MarketplaceEntitlementActions.Grant));

        Assert.True(applied);
        Assert.True(await entitlements.IsEntitledAsync("voice", "workspace-a", "tenant-1"));
    }

    [Fact]
    public async Task Revoke_DeactivatesEntitlement()
    {
        var (applier, entitlements) = CreateApplier();
        await applier.ApplyAsync(NewEvent("evt-1", MarketplaceEntitlementActions.Grant));

        var applied = await applier.ApplyAsync(NewEvent("evt-2", MarketplaceEntitlementActions.Revoke));

        Assert.True(applied);
        Assert.False(await entitlements.IsEntitledAsync("voice", "workspace-a", "tenant-1"));
    }

    [Fact]
    public async Task ReplayedEvent_IsSkippedIdempotently()
    {
        var (applier, entitlements) = CreateApplier();
        await applier.ApplyAsync(NewEvent("evt-1", MarketplaceEntitlementActions.Grant));

        // Replay desselben Events mit inzwischen widersprüchlicher Wirkung darf nichts ändern.
        var replayApplied = await applier.ApplyAsync(NewEvent("evt-1", MarketplaceEntitlementActions.Revoke));

        Assert.False(replayApplied);
        Assert.True(await entitlements.IsEntitledAsync("voice", "workspace-a", "tenant-1"));
    }

    [Fact]
    public async Task UnsupportedAction_Throws()
    {
        var (applier, _) = CreateApplier();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            applier.ApplyAsync(NewEvent("evt-1", "upgrade")));
    }

    [Fact]
    public async Task JobHandler_DeserializesPayload_AndApplies()
    {
        var (applier, entitlements) = CreateApplier();
        var handler = new MarketplaceEntitlementSyncJobHandler(applier);
        var payloadJson = """{"eventId":"evt-9","action":"grant","pluginId":"voice","tenantKey":"tenant-1","workspaceKey":"workspace-a"}""";

        await handler.ExecuteAsync(new BackgroundJobExecutionContext(
            Guid.NewGuid(),
            MarketplaceEntitlementSyncJobHandler.JobTypeName,
            payloadJson,
            "workspace-a",
            Attempt: 1));

        Assert.True(await entitlements.IsEntitledAsync("voice", "workspace-a", "tenant-1"));
    }

    private static (MarketplaceEntitlementApplier Applier, InMemoryPluginEntitlementStore Entitlements) CreateApplier()
    {
        var entitlements = new InMemoryPluginEntitlementStore(new BackendHostOptions());
        var applier = new MarketplaceEntitlementApplier(
            new InMemoryMarketplaceEntitlementEventStore(),
            entitlements,
            new InMemoryHostAuditStore());
        return (applier, entitlements);
    }

    private static MarketplaceEntitlementEventPayload NewEvent(string eventId, string action) =>
        new(eventId, action, "voice", "tenant-1", "workspace-a");
}
