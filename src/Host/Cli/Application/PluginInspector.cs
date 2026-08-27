using Callora.Core.Extensibility;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Callora.Host.Cli.Application;

/// <summary>
/// Reports what a plugin package declares and what it attaches to, without a running host.
/// </summary>
/// <remarks>
/// <para>
/// The runtime knows all of this once a plugin is installed — the extension-point registry
/// and the route inventory hold it. What was missing is the answer at the moment it is worth
/// having: before installing, from a file on disk, with no host and no database.
/// </para>
/// <para>
/// Extension points are matched by type NAME, not identity. The plugin is loaded into its own
/// collectible context, so its view of <c>IBusinessEventListener</c> is a different
/// <see cref="Type"/> instance from this process's — the same reason
/// <see cref="PluginContractTester"/> compares <c>FullName</c>.
/// </para>
/// </remarks>
internal sealed class PluginInspector
{
    public Task<PluginInspectionResult> InspectAsync(
        PluginInspectRequest request,
        CancellationToken cancellationToken)
    {
        var assemblyPath = Path.GetFullPath(request.AssemblyPath);
        if (!File.Exists(assemblyPath))
        {
            return Task.FromResult(PluginInspectionResult.Fail(
                $"Plugin assembly was not found: '{assemblyPath}'."));
        }

        var registryPath = string.IsNullOrWhiteSpace(request.RegistryPath)
            ? Path.Combine(Path.GetDirectoryName(assemblyPath) ?? ".", "registry.json")
            : Path.GetFullPath(request.RegistryPath);

        var report = new StringBuilder();
        AppendManifest(report, registryPath, cancellationToken);
        AppendAttachments(report, assemblyPath);

        return Task.FromResult(PluginInspectionResult.Ok(report.ToString()));
    }

    private static void AppendManifest(StringBuilder report, string registryPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(registryPath))
        {
            // Reported rather than fatal: the assembly still says what it attaches to, and
            // "no manifest here" is itself the answer when someone inspects build output.
            report.AppendLine($"Manifest:   (none at {registryPath})");
            return;
        }

        PluginInspectionManifest? manifest;
        try
        {
            var json = File.ReadAllText(registryPath);
            manifest = JsonSerializer.Deserialize<PluginInspectionManifest>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            report.AppendLine($"Manifest:   (unreadable: {exception.Message})");
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (manifest is null)
        {
            report.AppendLine("Manifest:   (empty)");
            return;
        }

        report.AppendLine($"Plugin:     {manifest.Name} ({manifest.PluginId}) {manifest.Version}");
        report.AppendLine($"Contract:   {manifest.ContractVersion}");
        report.AppendLine($"Entry type: {manifest.EntryTypeName}");
        AppendList(report, "Provides", manifest.Capabilities);
        AppendList(report, "Requires", manifest.RequiresCapabilities);
        AppendList(report, "Permissions", manifest.Permissions?.Select(x => x?.Key).ToArray());
        AppendList(
            report,
            "Depends on",
            manifest.Dependencies?.Select(pair => $"{pair.Key} {pair.Value}").ToArray());
    }

    private static void AppendAttachments(StringBuilder report, string assemblyPath)
    {
        var extensionPoints = ExtensionPointNames();
        var loadContext = new PluginInspectionLoadContext(assemblyPath);
        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            var attached = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var type in LoadableTypes(assembly))
            {
                foreach (var contract in type.GetInterfaces())
                {
                    if (contract.FullName is { } name && extensionPoints.Contains(name))
                    {
                        attached.Add($"{Short(name)}  ({type.Name})");
                    }
                }
            }

            report.AppendLine();
            report.AppendLine(attached.Count == 0
                ? "Attaches to: (nothing the host sanctions)"
                : "Attaches to:");
            foreach (var entry in attached)
            {
                report.AppendLine($"  {entry}");
            }
        }
        finally
        {
            loadContext.Unload();
        }
    }

    /// <summary>Every <c>[CalloraExtensible]</c> interface, by full name.</summary>
    private static HashSet<string> ExtensionPointNames() =>
        typeof(CalloraExtensibleAttribute).Assembly
            .GetExportedTypes()
            .Where(type => type.IsInterface &&
                           type.GetCustomAttribute<CalloraExtensibleAttribute>(inherit: false) is not null)
            .Select(type => type.FullName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

    // A plugin may reference assemblies this process cannot resolve; the types that did load
    // are still worth reporting. Failing the whole inspection over one unresolved reference
    // would make the command useless exactly where it is most needed — an unfamiliar package.
    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }

    private static void AppendList(StringBuilder report, string label, IReadOnlyCollection<string?>? values)
    {
        var present = (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (present.Length == 0)
        {
            return;
        }

        report.AppendLine($"{label + ":",-12}{string.Join(", ", present)}");
    }

    private static string Short(string fullName) =>
        fullName[(fullName.LastIndexOf('.') + 1)..];

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };
}
