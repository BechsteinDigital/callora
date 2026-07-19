using Callora.Core.Application.Media;
using Callora.Core.Application.Media.Events;
using Xunit;

namespace Callora.Core.Tests.Application.Media.Events;

public sealed class MediaBusinessEventTests
{
    private static MediaItemSnapshot Item() =>
        new(
            Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            WorkspaceKey: "acme",
            FileName: "greeting.wav",
            ContentType: "audio/wav",
            SizeBytes: 2048,
            Folder: "announcements",
            CreatedBy: "alice",
            CreatedAtUtc: new DateTimeOffset(2026, 7, 19, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Uploaded_CarriesTheAssetFields_AndWorkspaceScope()
    {
        var sut = MediaBusinessEvent.Uploaded(Item());

        Assert.Equal(MediaEventTypes.Uploaded, sut.EventName);
        Assert.Equal("acme", sut.WorkspaceKey);

        var data = sut.ToEventData();
        Assert.Equal("11111111-1111-1111-1111-111111111111", data["mediaId"]);
        Assert.Equal("greeting.wav", data["fileName"]);
        Assert.Equal("audio/wav", data["contentType"]);
        Assert.Equal("announcements", data["folder"]);
        Assert.Equal("2048", data["sizeBytes"]);
    }

    [Fact]
    public void Deleted_UsesTheDeletedEventName()
    {
        var sut = MediaBusinessEvent.Deleted(Item());

        Assert.Equal(MediaEventTypes.Deleted, sut.EventName);
        Assert.Equal("acme", sut.WorkspaceKey);
    }

    [Fact]
    public void Provider_DescribesUploadedAndDeleted()
    {
        var names = new MediaBusinessEventProvider().GetDescriptors()
            .Select(static d => d.EventName)
            .ToArray();

        Assert.Contains(MediaEventTypes.Uploaded, names);
        Assert.Contains(MediaEventTypes.Deleted, names);
    }
}
