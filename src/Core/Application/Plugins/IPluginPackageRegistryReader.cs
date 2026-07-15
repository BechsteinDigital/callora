namespace Callora.Core.Application.Plugins;

public interface IPluginPackageRegistryReader
{
    ValueTask<PluginPackageRegistryReadResult> ReadForAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default);
}
