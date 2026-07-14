namespace Callora.Host.Backend.Application.Plugins;

public interface IPluginPackageRegistryReader
{
    ValueTask<PluginPackageRegistryReadResult> ReadForAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default);
}
