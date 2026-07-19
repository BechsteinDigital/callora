using Callora.Core.Application.Security.Events;
using Xunit;

namespace Callora.Core.Tests.Application.Security.Events;

public sealed class UserBusinessEventTests
{
    [Fact]
    public void Created_CarriesTheIdentity_AndIsPlatformWide()
    {
        var sut = UserBusinessEvent.Created("alice", "alice@x", "Alice");

        Assert.Equal(UserEventTypes.Created, sut.EventName);
        Assert.Null(sut.WorkspaceKey); // users are global

        var data = sut.ToEventData();
        Assert.Equal("alice", data["userId"]);
        Assert.Equal("alice@x", data["email"]);
        Assert.Equal("Alice", data["displayName"]);
    }

    [Fact]
    public void Updated_UsesTheUpdatedEventName()
    {
        var sut = UserBusinessEvent.Updated("alice", null, null);

        Assert.Equal(UserEventTypes.Updated, sut.EventName);
        Assert.Equal("alice", sut.ToEventData()["userId"]);
        Assert.Equal(string.Empty, sut.ToEventData()["email"]);
    }

    [Fact]
    public void Deleted_CarriesOnlyTheUserId()
    {
        var sut = UserBusinessEvent.Deleted("alice");

        Assert.Equal(UserEventTypes.Deleted, sut.EventName);
        var data = sut.ToEventData();
        Assert.Equal("alice", data["userId"]);
        Assert.False(data.ContainsKey("email"));
    }

    [Fact]
    public void Provider_DescribesCreatedUpdatedAndDeleted()
    {
        var names = new UserBusinessEventProvider().GetDescriptors()
            .Select(static d => d.EventName)
            .ToArray();

        Assert.Contains(UserEventTypes.Created, names);
        Assert.Contains(UserEventTypes.Updated, names);
        Assert.Contains(UserEventTypes.Deleted, names);
    }
}
