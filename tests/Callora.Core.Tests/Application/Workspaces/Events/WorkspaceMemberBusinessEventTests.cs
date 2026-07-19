using Callora.Core.Application.Workspaces;
using Callora.Core.Application.Workspaces.Events;
using Xunit;

namespace Callora.Core.Tests.Application.Workspaces.Events;

public sealed class WorkspaceMemberBusinessEventTests
{
    private static WorkspaceMemberSnapshot Member(string role) =>
        new("acme", "alice", "alice@x", "Alice", role, new DateTimeOffset(2026, 7, 19, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Assigned_CarriesTheMembershipFields()
    {
        var sut = WorkspaceMemberBusinessEvent.Assigned(Member("owner"));

        Assert.Equal(WorkspaceMemberEventTypes.Assigned, sut.EventName);
        Assert.Equal("acme", sut.WorkspaceKey);

        var data = sut.ToEventData();
        Assert.Equal("alice", data["userId"]);
        Assert.Equal("owner", data["role"]);
        Assert.Equal("alice@x", data["email"]);
        Assert.Equal("Alice", data["displayName"]);
    }

    [Fact]
    public void Removed_CarriesOnlyTheIdentity()
    {
        var sut = WorkspaceMemberBusinessEvent.Removed("acme", "alice");

        Assert.Equal(WorkspaceMemberEventTypes.Removed, sut.EventName);
        Assert.Equal("acme", sut.WorkspaceKey);

        var data = sut.ToEventData();
        Assert.Equal("acme", data["workspaceKey"]);
        Assert.Equal("alice", data["userId"]);
        // A removed membership no longer exists — no role/email is carried.
        Assert.False(data.ContainsKey("role"));
    }

    [Fact]
    public void Provider_DescribesMembershipEvents()
    {
        var names = new WorkspaceBusinessEventProvider().GetDescriptors()
            .Select(static d => d.EventName)
            .ToArray();

        Assert.Contains(WorkspaceMemberEventTypes.Assigned, names);
        Assert.Contains(WorkspaceMemberEventTypes.Removed, names);
    }
}
