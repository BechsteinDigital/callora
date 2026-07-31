using Callora.Core.Infrastructure.Plugins;

namespace Callora.Core.Tests.Infrastructure.Plugins;

public sealed class PluginUiAssetDirectoryOperationsTests
{
    [Fact]
    public void MoveDirectory_CrossDeviceRename_CopiesTreeAndDeletesSource()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"callora-plugin-assets-move-{Guid.NewGuid():N}");
        var source = Path.Combine(tempRoot, "source");
        var destination = Path.Combine(tempRoot, "destination");
        var nestedSource = Path.Combine(source, "nested");
        Directory.CreateDirectory(nestedSource);
        File.WriteAllText(Path.Combine(source, "main.js"), "admin");
        File.WriteAllText(Path.Combine(nestedSource, "main.css"), "style");

        try
        {
            PluginUiAssetDirectoryOperations.MoveDirectory(
                source,
                destination,
                static (_, _) => throw new IOException("Invalid cross-device link", 18));

            Assert.False(Directory.Exists(source));
            Assert.Equal("admin", File.ReadAllText(Path.Combine(destination, "main.js")));
            Assert.Equal("style", File.ReadAllText(Path.Combine(destination, "nested", "main.css")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MoveDirectory_NonCrossDeviceIoFailure_IsNotHidden()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"callora-plugin-assets-move-{Guid.NewGuid():N}");
        var source = Path.Combine(tempRoot, "source");
        var destination = Path.Combine(tempRoot, "destination");
        Directory.CreateDirectory(source);

        try
        {
            var expected = new IOException("Permission denied.");

            var actual = Assert.Throws<IOException>(() =>
                PluginUiAssetDirectoryOperations.MoveDirectory(
                    source,
                    destination,
                    (_, _) => throw expected));

            Assert.Same(expected, actual);
            Assert.True(Directory.Exists(source));
            Assert.False(Directory.Exists(destination));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
