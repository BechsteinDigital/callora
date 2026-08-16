using Callora.Core.Application.Plugins;
using Callora.Core.Tests.Support;
using System.Reflection;
using System.Reflection.Emit;

namespace Callora.Core.Tests.Hosting;

public sealed class SharedContractAssemblyRegistryTests
{
    [Fact]
    public void RegisterFromTwoPlugins_SharesOneAssemblyIdentity()
    {
        var contractName = UniqueName();
        using var workspace = new TempWorkspace();
        var pluginA = workspace.CreateDirectory("plugin-a");
        var pluginB = workspace.CreateDirectory("plugin-b");
        EmitContractAssembly(Path.Combine(pluginA, $"{contractName}.dll"), contractName, new Version(1, 0, 0, 0));
        File.Copy(
            Path.Combine(pluginA, $"{contractName}.dll"),
            Path.Combine(pluginB, $"{contractName}.dll"));

        var registry = new SharedContractAssemblyRegistry();
        registry.RegisterContracts(pluginA, [$"{contractName}.dll"]);
        registry.RegisterContracts(pluginB, [$"{contractName}.dll"]);

        var resolved = registry.TryResolve(new AssemblyName(contractName));
        Assert.NotNull(resolved);
        Assert.Same(resolved, registry.TryResolve(new AssemblyName(contractName)));
    }

    [Fact]
    public void Register_WithIncompatibleMajorVersion_Throws()
    {
        var contractName = UniqueName();
        using var workspace = new TempWorkspace();
        var pluginA = workspace.CreateDirectory("plugin-a");
        var pluginB = workspace.CreateDirectory("plugin-b");
        EmitContractAssembly(Path.Combine(pluginA, $"{contractName}.dll"), contractName, new Version(1, 2, 0, 0));
        EmitContractAssembly(Path.Combine(pluginB, $"{contractName}.dll"), contractName, new Version(2, 0, 0, 0));

        var registry = new SharedContractAssemblyRegistry();
        registry.RegisterContracts(pluginA, [$"{contractName}.dll"]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterContracts(pluginB, [$"{contractName}.dll"]));
        Assert.Contains("incompatible major version", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_WithForeignMajorVersion_FallsBackToNull()
    {
        var contractName = UniqueName();
        using var workspace = new TempWorkspace();
        var plugin = workspace.CreateDirectory("plugin");
        EmitContractAssembly(Path.Combine(plugin, $"{contractName}.dll"), contractName, new Version(1, 0, 0, 0));

        var registry = new SharedContractAssemblyRegistry();
        registry.RegisterContracts(plugin, [$"{contractName}.dll"]);

        Assert.Null(registry.TryResolve(new AssemblyName($"{contractName}, Version=2.0.0.0")));
        Assert.NotNull(registry.TryResolve(new AssemblyName($"{contractName}, Version=1.9.0.0")));
    }

    [Fact]
    public void Register_WithTraversalPath_Throws()
    {
        using var workspace = new TempWorkspace();
        var plugin = workspace.CreateDirectory("plugin");

        var registry = new SharedContractAssemblyRegistry();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterContracts(plugin, ["../outside.dll"]));
        Assert.Contains("escapes the plugin directory", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_WithMissingContractFile_Throws()
    {
        using var workspace = new TempWorkspace();
        var plugin = workspace.CreateDirectory("plugin");

        var registry = new SharedContractAssemblyRegistry();
        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterContracts(plugin, ["missing.dll"]));
        Assert.Contains("missing from the plugin package", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginLoadContexts_ResolveSharedContractToSameAssembly()
    {
        var contractName = UniqueName();
        using var workspace = new TempWorkspace();
        var plugin = workspace.CreateDirectory("plugin");
        var contractPath = Path.Combine(plugin, $"{contractName}.dll");
        EmitContractAssembly(contractPath, contractName, new Version(1, 0, 0, 0));

        var registry = new SharedContractAssemblyRegistry();
        registry.RegisterContracts(plugin, [$"{contractName}.dll"]);

        var contextA = new PluginAssemblyLoadContext(contractPath, registry);
        var contextB = new PluginAssemblyLoadContext(contractPath, registry);
        try
        {
            var fromA = contextA.LoadFromAssemblyName(new AssemblyName(contractName));
            var fromB = contextB.LoadFromAssemblyName(new AssemblyName(contractName));

            Assert.Same(fromA, fromB);
        }
        finally
        {
            contextA.Unload();
            contextB.Unload();
        }
    }

    [Fact]
    public void ManifestReader_ReadsContractsArray_AndToleratesAbsence()
    {
        using var workspace = new TempWorkspace();
        var plugin = workspace.CreateDirectory("plugin");
        var assemblyPath = Path.Combine(plugin, "plugin.dll");
        File.WriteAllText(assemblyPath, "stub");
        File.WriteAllText(
            Path.Combine(plugin, "registry.json"),
            """{ "pluginId": "acme", "contracts": ["Acme.Chat.Contracts.dll", " "] }""");

        var declared = PluginContractManifestReader.ReadDeclaredContracts(assemblyPath);
        var single = Assert.Single(declared);
        Assert.Equal("Acme.Chat.Contracts.dll", single);

        File.WriteAllText(Path.Combine(plugin, "registry.json"), """{ "pluginId": "acme" }""");
        Assert.Empty(PluginContractManifestReader.ReadDeclaredContracts(assemblyPath));
    }

    [Fact]
    public void Register_WithPluginProvidedCalloraContract_SharesItInsteadOfRefusingTheName()
    {
        // Interne Plugins tragen dasselbe Präfix wie die Plattform (ADR-025). Bis 08/2026 wies
        // die Registry so eine Deklaration ab und verlangte einen anderen Namen — mit der
        // Begründung, der Ladekontext schicke Callora-Namen ohnehin in den Default-Kontext.
        // Seit dieser Frühausstieg weg ist, trägt die Begründung nicht mehr: Was der Host nicht
        // stellt, ist plugin-eigen und gehört geteilt, unabhängig davon, wie es heißt.
        var contractName = $"Callora.Plugin.Fake.Abstractions.{Guid.NewGuid():N}";
        using var workspace = new TempWorkspace();
        var plugin = workspace.CreateDirectory("plugin");
        EmitContractAssembly(Path.Combine(plugin, $"{contractName}.dll"), contractName, new Version(1, 0, 0, 0));

        var registry = new SharedContractAssemblyRegistry();
        registry.RegisterContracts(plugin, [$"{contractName}.dll"], "acme");

        var registration = Assert.Single(registry.ListRegistrations());
        Assert.Equal(contractName, registration.AssemblyName);
        Assert.False(registration.IsHostProvided);
        Assert.NotNull(registry.TryResolve(new AssemblyName(contractName)));
    }

    [Fact]
    public void Register_WithHostProvidedContractOutsideThePrefix_IsRecordedNotLoaded()
    {
        // Die Gegenrichtung derselben Änderung, und die Lücke, die das Präfix offenließ: Vor
        // 08/2026 fand für Namen OHNE "Callora." gar keine Host-Prüfung statt. Ein Plugin, das
        // eine Framework-Assembly unter contracts deklarierte, bekam seine Kopie in den
        // Default-Kontext geladen — neben die des Hosts, mit doppelter Typidentität.
        using var workspace = new TempWorkspace();
        var plugin = workspace.CreateDirectory("plugin");
        var hostAssembly = typeof(Microsoft.Extensions.Logging.ILogger).Assembly;
        var fileName = Path.GetFileName(hostAssembly.Location);
        File.Copy(hostAssembly.Location, Path.Combine(plugin, fileName));

        var registry = new SharedContractAssemblyRegistry();
        registry.RegisterContracts(plugin, [fileName], "acme");

        var registration = Assert.Single(registry.ListRegistrations());
        Assert.True(registration.IsHostProvided);
        // Aufgezeichnet, aber nicht geteilt: Der Ladekontext fällt danach auf den
        // Default-Kontext und damit auf die Kopie des Hosts.
        Assert.Null(registry.TryResolve(new AssemblyName(registration.AssemblyName)));
    }

    [Fact]
    public void Register_WithHostProvidedCalloraContract_IsRecordedNotLoaded()
    {
        using var workspace = new TempWorkspace();
        var plugin = workspace.CreateDirectory("plugin");
        // Callora.Core is loaded in this process, so it stands in for a contract the host app
        // references: declaring it is legitimate and must not fail.
        var source = typeof(SharedContractAssemblyRegistry).Assembly.Location;
        File.Copy(source, Path.Combine(plugin, "Callora.Core.dll"));

        var registry = new SharedContractAssemblyRegistry();
        registry.RegisterContracts(plugin, ["Callora.Core.dll"], "acme");

        var registration = Assert.Single(registry.ListRegistrations());
        Assert.Equal("Callora.Core", registration.AssemblyName);
        Assert.True(registration.IsHostProvided);
        Assert.Equal("acme", registration.DeclaringPluginId);
    }

    [Fact]
    public void ListRegistrations_NamesThePluginThatBroughtTheContract()
    {
        var contractName = UniqueName();
        using var workspace = new TempWorkspace();
        var plugin = workspace.CreateDirectory("plugin");
        EmitContractAssembly(Path.Combine(plugin, $"{contractName}.dll"), contractName, new Version(2, 1, 0, 0));

        var registry = new SharedContractAssemblyRegistry();
        registry.RegisterContracts(plugin, [$"{contractName}.dll"], "acme.chat");

        var registration = Assert.Single(registry.ListRegistrations());
        Assert.Equal(contractName, registration.AssemblyName);
        Assert.Equal("2.1.0.0", registration.Version);
        Assert.Equal("acme.chat", registration.DeclaringPluginId);
        Assert.False(registration.IsHostProvided);
    }

    [Fact]
    public void PluginLoadContext_StillResolvesAHostProvidedCalloraAssemblyToTheHostCopy()
    {
        // Die Zusage, an der ADR-012 hängt und die der entfernte Frühausstieg getragen hat:
        // Host und Plugin sehen für Plattformtypen dieselbe Assembly. Sie ruht jetzt auf dem
        // Default-Fallback statt auf dem Namen — dieser Test ist der Beleg, dass sie hält.
        var contractName = UniqueName();
        using var workspace = new TempWorkspace();
        var plugin = workspace.CreateDirectory("plugin");
        var anchorPath = Path.Combine(plugin, $"{contractName}.dll");
        EmitContractAssembly(anchorPath, contractName, new Version(1, 0, 0, 0));

        var context = new PluginAssemblyLoadContext(anchorPath, new SharedContractAssemblyRegistry());
        try
        {
            var resolved = context.LoadFromAssemblyName(new AssemblyName("Callora.Core"));

            Assert.Same(typeof(SharedContractAssemblyRegistry).Assembly, resolved);
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public void PluginLoadContext_ResolvesAPluginProvidedCalloraContractInsteadOfFailing()
    {
        // Der Fall, der vorher in einer FileNotFoundException endete: Der Kontext schickte den
        // Namen wegen des Präfix in den Default-Kontext, und dort lag nichts.
        var contractName = $"Callora.Plugin.Fake.Abstractions.{Guid.NewGuid():N}";
        using var workspace = new TempWorkspace();
        var plugin = workspace.CreateDirectory("plugin");
        var contractPath = Path.Combine(plugin, $"{contractName}.dll");
        EmitContractAssembly(contractPath, contractName, new Version(1, 0, 0, 0));

        var registry = new SharedContractAssemblyRegistry();
        registry.RegisterContracts(plugin, [$"{contractName}.dll"], "acme");

        var context = new PluginAssemblyLoadContext(contractPath, registry);
        try
        {
            var resolved = context.LoadFromAssemblyName(new AssemblyName(contractName));

            Assert.NotNull(resolved);
            Assert.Same(registry.TryResolve(new AssemblyName(contractName)), resolved);
        }
        finally
        {
            context.Unload();
        }
    }

    private static string UniqueName() => $"Acme.Fake.Contracts.{Guid.NewGuid():N}";

    private static void EmitContractAssembly(string path, string assemblyName, Version version)
    {
        var builder = new PersistedAssemblyBuilder(
            new AssemblyName(assemblyName) { Version = version },
            typeof(object).Assembly);
        var module = builder.DefineDynamicModule(assemblyName);
        var type = module.DefineType(
            $"{assemblyName}.IWidget",
            TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        type.CreateType();
        builder.Save(path);
    }
}
