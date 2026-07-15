using System.Text;
using Callora.Core.Application.Media;
using Callora.Core.Application.Policies;
using Callora.Core.Infrastructure.Media;
using Xunit;

namespace Callora.Core.Tests.Application.Media;

public sealed class MediaSubsystemTests
{
    [Theory]
    [InlineData("audio/wav", true)]
    [InlineData("audio/mpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("application/x-msdownload", false)]
    [InlineData("text/html", false)]
    [InlineData(null, false)]
    public void UploadPolicy_WhitelistsContentTypes(string? contentType, bool expected)
    {
        Assert.Equal(expected, MediaUploadPolicy.IsAllowedContentType(contentType));
    }

    [Fact]
    public void UploadPolicy_EnforcesSizeBounds()
    {
        Assert.False(MediaUploadPolicy.IsAllowedSize(0));
        Assert.True(MediaUploadPolicy.IsAllowedSize(1024));
        Assert.False(MediaUploadPolicy.IsAllowedSize(MediaUploadPolicy.MaxSizeBytes + 1));
    }

    [Fact]
    public async Task FileSystemStorage_RoundtripsAndDeletesById()
    {
        var root = Path.Combine(Path.GetTempPath(), "callora-media-tests", Guid.NewGuid().ToString("N"));
        var storage = new FileSystemMediaStorage(new BackendHostOptions { MediaStoragePath = root });
        var mediaId = Guid.NewGuid();

        await storage.WriteAsync(mediaId, new MemoryStream(Encoding.UTF8.GetBytes("audio-bytes")));

        await using (var read = await storage.OpenReadAsync(mediaId))
        {
            Assert.NotNull(read);
            using var reader = new StreamReader(read!);
            Assert.Equal("audio-bytes", await reader.ReadToEndAsync());
        }

        await storage.DeleteAsync(mediaId);
        Assert.Null(await storage.OpenReadAsync(mediaId));

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task FileSystemStorage_UnknownId_ReturnsNull()
    {
        var root = Path.Combine(Path.GetTempPath(), "callora-media-tests", Guid.NewGuid().ToString("N"));
        var storage = new FileSystemMediaStorage(new BackendHostOptions { MediaStoragePath = root });

        Assert.Null(await storage.OpenReadAsync(Guid.NewGuid()));
    }
}
