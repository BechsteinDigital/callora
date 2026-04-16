namespace Callora.Host.Backend.Application.Abstractions.Plugins;

public interface INuGetPluginAssemblyResolver
{
    ValueTask<NuGetPluginAssemblyResolveResult> ResolveAsync(
        string packageId,
        string packageVersion,
        string? assemblyFileName,
        CancellationToken cancellationToken = default);
}

public sealed record NuGetPluginAssemblyResolveResult(
    bool IsSuccess,
    string? AssemblyPath,
    string? Message = null)
{
    public static NuGetPluginAssemblyResolveResult Success(string assemblyPath) =>
        new(true, assemblyPath, null);

    public static NuGetPluginAssemblyResolveResult Failure(string message) =>
        new(false, null, message);
}
