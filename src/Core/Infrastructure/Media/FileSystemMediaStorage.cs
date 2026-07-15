using Callora.Core.Application.Media;
using Callora.Core.Application.Policies;

namespace Callora.Core.Infrastructure.Media;

/// <summary>
/// Stores media bytes as "&lt;mediaId&gt;.bin" under the configured root —
/// ids are the only path component, so path traversal is impossible.
/// </summary>
public sealed class FileSystemMediaStorage(BackendHostOptions options) : IMediaStorage
{
    private string RootPath => string.IsNullOrWhiteSpace(options.MediaStoragePath)
        ? Path.Combine(AppContext.BaseDirectory, "media")
        : options.MediaStoragePath;

    public async Task WriteAsync(Guid mediaId, Stream content, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(RootPath);
        await using var target = File.Create(PathFor(mediaId));
        await content.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
    }

    public Task<Stream?> OpenReadAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        var path = PathFor(mediaId);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        var path = PathFor(mediaId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string PathFor(Guid mediaId) =>
        Path.Combine(RootPath, mediaId.ToString("N") + ".bin");
}
