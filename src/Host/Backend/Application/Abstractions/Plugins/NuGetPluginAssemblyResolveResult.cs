namespace Callora.Host.Backend.Application.Abstractions.Plugins;

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
