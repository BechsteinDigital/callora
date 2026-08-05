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
    public void Register_WithUnprovidedCalloraPrefix_ThrowsInsteadOfSkipping()
    {
        var contractName = $"Callora.Fake.Contracts.{Guid.NewGuid():N}";
        using var workspace = new TempWorkspace();
        var plugin = workspace.CreateDirectory("plugin");
        EmitContractAssembly(Path.Combine(plugin, $"{contractName}.dll"), contractName, new Version(1, 0, 0, 0));

        var registry = new SharedContractAssemblyRegistry();

        // The prefix means "the host provides this", and the plugin load context delegates those
        // names to the default context. A plugin-provided contract carrying it would be skipped
        // here AND absent there, so it would fail to load at plugin start. Fail-closed instead.
        var error = Assert.Throws<InvalidOperationException>(
            () => registry.RegisterContracts(plugin, [$"{contractName}.dll"], "acme"));

        Assert.Contains("reserved 'Callora.' prefix", error.Message, StringComparison.Ordinal);
        Assert.Empty(registry.ListRegistrations());
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
