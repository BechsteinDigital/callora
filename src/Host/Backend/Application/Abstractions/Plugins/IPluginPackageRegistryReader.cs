namespace Callora.Host.Backend.Application.Abstractions.Plugins;

public interface IPluginPackageRegistryReader
{
    ValueTask<PluginPackageRegistryReadResult> ReadForAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default);
}
