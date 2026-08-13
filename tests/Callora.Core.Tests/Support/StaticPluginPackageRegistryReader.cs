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

    /// <summary>
    /// Declares one plugin's tier and capabilities at an assembly path — for callers that test the
    /// activation ORDER rather than the dependency gate.
    /// </summary>
    /// <param name="assemblyPath">Path the installation record points at.</param>
    /// <param name="tier">Raw manifest value ("system"/"application"/null), unresolved.</param>
    /// <param name="capabilities">Capabilities this plugin provides.</param>
    /// <param name="requiredCapabilities">Capabilities it needs first.</param>
    public void AddMetadata(
        string assemblyPath,
        string? tier = null,
        IReadOnlyList<string>? capabilities = null,
        IReadOnlyList<string>? requiredCapabilities = null) =>
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
                capabilities ?? [],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                RequiredCapabilities: requiredCapabilities,
                Tier: tier));

    public ValueTask<PluginPackageRegistryReadResult> ReadForAssemblyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        LastAssemblyPath = assemblyPath;
        return ValueTask.FromResult(
            _byPath.TryGetValue(assemblyPath, out var mapped) ? mapped : Result);
    }
}
