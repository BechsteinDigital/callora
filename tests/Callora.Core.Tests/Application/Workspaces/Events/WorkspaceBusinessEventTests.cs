using Callora.Core.Application.Workspaces;
using Callora.Core.Application.Workspaces.Events;
using Xunit;

namespace Callora.Core.Tests.Application.Workspaces.Events;

public sealed class WorkspaceBusinessEventTests
{
    private static readonly DateTimeOffset Stamp = new(2026, 7, 19, 10, 0, 0, TimeSpan.Zero);

    private static WorkspaceSnapshot Snapshot(DateTimeOffset created, DateTimeOffset updated) =>
        new(
            TenantKey: "tenant",
            WorkspaceKey: "acme",
            DisplayName: "Acme Corp",
            WorkspaceType: "standard",
            IsActive: true,
            TenantIsActive: true,
            PublicHost: null,
            ThemePluginId: null,
            ThemeVersion: null,
            ThemeAssignedBy: null,
            ThemeAssignedAtUtc: null,
            CreatedAtUtc: created,
            UpdatedAtUtc: updated);

    [Fact]
    public void ForUpsert_WithEqualTimestamps_IsCreated()
    {
        // A freshly inserted workspace has equal created/updated stamps.
        var sut = WorkspaceBusinessEvent.ForUpsert(Snapshot(Stamp, Stamp));

        Assert.Equal(WorkspaceEventTypes.Created, sut.EventName);
        Assert.Equal("acme", sut.WorkspaceKey);
    }

    [Fact]
    public void ForUpsert_WithLaterUpdateStamp_IsUpdated()
    {
        var sut = WorkspaceBusinessEvent.ForUpsert(Snapshot(Stamp, Stamp.AddMinutes(5)));

        Assert.Equal(WorkspaceEventTypes.Updated, sut.EventName);
    }

    [Fact]
    public void ForDeletion_IsDeleted()
    {
        var sut = WorkspaceBusinessEvent.ForDeletion(Snapshot(Stamp, Stamp));

        Assert.Equal(WorkspaceEventTypes.Deleted, sut.EventName);
    }

    [Fact]
    public void ToEventData_ProjectsTheWorkspaceFields()
    {
        var data = WorkspaceBusinessEvent.ForUpsert(Snapshot(Stamp, Stamp)).ToEventData();

        Assert.Equal("acme", data["workspaceKey"]);
        Assert.Equal("tenant", data["tenantKey"]);
        Assert.Equal("Acme Corp", data["displayName"]);
        Assert.Equal("standard", data["workspaceType"]);
        Assert.Equal("true", data["isActive"]);
    }

    [Fact]
    public void Provider_DescribesCreatedUpdatedAndDeleted()
    {
        var descriptors = new WorkspaceBusinessEventProvider().GetDescriptors();

        var names = descriptors.Select(static d => d.EventName).ToArray();
        Assert.Contains(WorkspaceEventTypes.Created, names);
        Assert.Contains(WorkspaceEventTypes.Updated, names);
        Assert.Contains(WorkspaceEventTypes.Deleted, names);
        Assert.All(descriptors, static d => Assert.NotEmpty(d.Fields));
    }
}
