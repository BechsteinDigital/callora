namespace Callora.Core.Infrastructure.Plugins;

internal static class PluginUiAssetDirectoryOperations
{
    private const int WindowsNotSameDeviceError = 17;
    private const int UnixCrossDeviceLinkError = 18;
    private const int WindowsNotSameDeviceHResult = unchecked((int)0x80070011);
    private const int UnixCrossDeviceLinkHResult = unchecked((int)0x80070012);

    internal static void MoveDirectory(
        string sourcePath,
        string destinationPath,
        Action<string, string>? moveDirectory = null)
    {
        var move = moveDirectory ?? Directory.Move;
        try
        {
            move(sourcePath, destinationPath);
        }
        catch (IOException exception) when (IsCrossDeviceMove(exception))
        {
            CopyThenMoveFromDestinationFileSystem(sourcePath, destinationPath);
        }
    }

    private static bool IsCrossDeviceMove(IOException exception) =>
        exception.HResult is
            WindowsNotSameDeviceError or
            UnixCrossDeviceLinkError or
            WindowsNotSameDeviceHResult or
            UnixCrossDeviceLinkHResult;

    private static void CopyThenMoveFromDestinationFileSystem(string sourcePath, string destinationPath)
    {
        var copyPath = destinationPath + $".copy-{Guid.NewGuid():N}";
        try
        {
            CopyDirectoryTree(sourcePath, copyPath);
            Directory.Move(copyPath, destinationPath);
            Directory.Delete(sourcePath, recursive: true);
        }
        catch
        {
            if (Directory.Exists(copyPath))
            {
                Directory.Delete(copyPath, recursive: true);
            }

            throw;
        }
    }

    private static void CopyDirectoryTree(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (var sourceDirectory in Directory.EnumerateDirectories(
                     sourcePath,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourcePath, sourceDirectory);
            Directory.CreateDirectory(Path.Combine(destinationPath, relativePath));
        }

        foreach (var sourceFile in Directory.EnumerateFiles(
                     sourcePath,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourcePath, sourceFile);
            File.Copy(sourceFile, Path.Combine(destinationPath, relativePath));
        }
    }
}
