using Callora.Core.Application.Plugins;

namespace Callora.Core.Tests.Support;

internal sealed class StaticPluginPackageRegistryReader : IPluginPackageRegistryReader
{
    private readonly Dictionary<string, PluginPackageRegistryReadResult> _byPath =
        new(StringComparer.Ordinal);

    public PluginPackageRegistryReadResult Result { get; set; } =
        new(false, true, null, null);

    public string? LastAssemblyPath { get; private set; }

    /// <summary>
    /// Declares one plugin's dependencies at an assembly path. Callers that need several plugins to
    /// read differently use this; a caller with one plugin can keep setting <see cref="Result"/>.
    /// </summary>
    /// <param name="assemblyPath">Path the installation record points at.</param>
    /// <param name="dependencies">Contract id to npm-style range.</param>
    public void AddDependencies(string assemblyPath, IReadOnlyDictionary<string, string> dependencies) =>
        _byPath[assemblyPath] = new PluginPackageRegistryReadResult(
            true,
            true,
            null,
            new PluginPackageRegistryMetadata(
                "v1",
                "1.0",
                "Test",
                Path.GetFileNameWithoutExtension(assemblyPath),
                "1.0.0",
                Path.GetFileName(assemblyPath),
                "Test.Plugin",
                [],
                dependencies));

    public ValueTask<PluginPackageRegistryReadResult> ReadForAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        LastAssemblyPath = assemblyPath;
        return ValueTask.FromResult(
            _byPath.TryGetValue(assemblyPath, out var mapped) ? mapped : Result);
    }
}
