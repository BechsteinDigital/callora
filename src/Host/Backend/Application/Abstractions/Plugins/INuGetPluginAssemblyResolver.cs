namespace Callora.Host.Backend.Application.Abstractions.Plugins;

public interface INuGetPluginAssemblyResolver
{
    ValueTask<NuGetPluginAssemblyResolveResult> ResolveAsync(
        string packageId,
        string packageVersion,
        string? assemblyFileName,
        CancellationToken cancellationToken = default);
}
