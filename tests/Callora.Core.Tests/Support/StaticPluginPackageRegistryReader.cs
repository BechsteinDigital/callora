using Callora.Core.Application.Plugins;

namespace Callora.Core.Tests.Support;

internal sealed class StaticPluginPackageRegistryReader : IPluginPackageRegistryReader
{
    public PluginPackageRegistryReadResult Result { get; set; } =
        new(false, true, null, null);

    public string? LastAssemblyPath { get; private set; }

    public ValueTask<PluginPackageRegistryReadResult> ReadForAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        LastAssemblyPath = assemblyPath;
        return ValueTask.FromResult(Result);
    }
}
