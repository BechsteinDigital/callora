using Callora.Host.Backend.Application.Plugins;

namespace Callora.Host.Backend.Tests.Support;

internal sealed class StaticNuGetPluginAssemblyResolver : INuGetPluginAssemblyResolver
{
    public NuGetPluginAssemblyResolveResult Result { get; set; } =
        NuGetPluginAssemblyResolveResult.Success("/tmp/plugin.dll");

    public ValueTask<NuGetPluginAssemblyResolveResult> ResolveAsync(
        string packageId,
        string packageVersion,
        string? assemblyFileName,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Result);
}
