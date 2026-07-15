namespace Callora.Core.Tests.Support;

/// <summary>
/// Disposable temp directory tree for filesystem-based tests.
/// </summary>
public sealed class TempWorkspace : IDisposable
{
    private readonly string _rootPath =
        Path.Combine(Path.GetTempPath(), $"callora-test-{Guid.NewGuid():N}");

    public string CreateDirectory(string relativePath)
    {
        var fullPath = Path.Combine(_rootPath, relativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }
}
