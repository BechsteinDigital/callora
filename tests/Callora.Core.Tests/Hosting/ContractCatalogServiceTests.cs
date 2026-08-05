using System.Reflection;
using System.Reflection.Emit;
using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Plugins;
using Callora.Core.Tests.Support;
using Xunit;

namespace Callora.Core.Tests.Hosting;

/// <summary>
/// The catalog joins two things the host already knew but never showed together: which contracts
/// are shared, and which plugins are bound to them. A version on its own says nothing about
/// consequences; a version plus its dependents says what an update would break.
/// </summary>
public sealed class ContractCatalogServiceTests
{
    [Fact]
    public async Task ASharedContractIsListedWithItsDeclaringPlugin()
    {
        using var workspace = new TempWorkspace();
        var contractName = UniqueName();
        var fixture = await FixtureAsync(workspace, contractName, new Version(1, 2, 0, 0), "acme.chat");

        var entry = Assert.Single(await fixture.ListAsync());

        Assert.Equal(contractName, entry.AssemblyName);
        Assert.Equal("1.2.0.0", entry.Version);
        Assert.Equal("acme.chat", entry.DeclaringPluginId);
        Assert.False(entry.IsHostProvided);
        // Stated rather than implied: pinning is why a contract change costs a restart.
        Assert.True(entry.RequiresRestartToChange);
    }

    [Fact]
    public async Task ADependentPluginAppearsWithItsRange()
    {
        using var workspace = new TempWorkspace();
        var contractName = UniqueName();
        var fixture = await FixtureAsync(
            workspace, contractName, new Version(1, 2, 0, 0), "acme.chat",
            dependents: [("crm", $">=1.0.0")]);

        var dependent = Assert.Single((await fixture.ListAsync())[0].Dependents);

        Assert.Equal("crm", dependent.PluginId);
        Assert.True(dependent.IsSatisfied);
    }

    [Fact]
    public async Task ADependentWhoseRangeTheVersionMissesIsMarkedUnsatisfied()
    {
        using var workspace = new TempWorkspace();
        var contractName = UniqueName();
        var fixture = await FixtureAsync(
            workspace, contractName, new Version(1, 2, 0, 0), "acme.chat",
            dependents: [("crm", ">=2.0.0")]);

        var dependent = Assert.Single((await fixture.ListAsync())[0].Dependents);

        // This is the answer an operator needs before replacing a contract: it names the plugin
        // that would break.
        Assert.False(dependent.IsSatisfied);
    }

    [Fact]
    public async Task AnUnreadableRangeCountsAsUnsatisfiedRatherThanFine()
    {
        using var workspace = new TempWorkspace();
        var contractName = UniqueName();
        var fixture = await FixtureAsync(
            workspace, contractName, new Version(1, 2, 0, 0), "acme.chat",
            dependents: [("crm", "not-a-range")]);

        Assert.False(Assert.Single((await fixture.ListAsync())[0].Dependents).IsSatisfied);
    }

    [Fact]
    public async Task ADependencyOnSomethingElseIsNotListedHere()
    {
        using var workspace = new TempWorkspace();
        var contractName = UniqueName();
        var fixture = await FixtureAsync(
            workspace, contractName, new Version(1, 2, 0, 0), "acme.chat",
            dependents: [("crm", ">=1.0.0")],
            dependencyContractName: "Some.Other.Contract");

        Assert.Empty((await fixture.ListAsync())[0].Dependents);
    }

    private static async Task<ContractCatalogService> FixtureAsync(
        TempWorkspace workspace,
        string contractName,
        Version version,
        string declaringPluginId,
        (string PluginId, string Range)[]? dependents = null,
        string? dependencyContractName = null)
    {
        var pluginDirectory = workspace.CreateDirectory($"plugin-{Guid.NewGuid():N}");
        EmitContractAssembly(Path.Combine(pluginDirectory, $"{contractName}.dll"), contractName, version);

        var registry = new SharedContractAssemblyRegistry();
        registry.RegisterContracts(pluginDirectory, [$"{contractName}.dll"], declaringPluginId);

        var installations = new InMemoryPluginInstallationRepository();
        var reader = new StaticPluginPackageRegistryReader();
        foreach (var (pluginId, range) in dependents ?? [])
        {
            var assemblyPath = Path.Combine(pluginDirectory, $"{pluginId}.dll");
            await installations.AddAsync(PluginInstallation.CreateInstalled(
                pluginId, pluginId, assemblyPath, null, DateTimeOffset.UtcNow));
            reader.AddDependencies(
                assemblyPath,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [dependencyContractName ?? contractName] = range,
                });
        }

        return new ContractCatalogService(registry, installations, reader);
    }

    private static string UniqueName() => $"Acme.Catalog.Contracts.{Guid.NewGuid():N}";

    private static void EmitContractAssembly(string path, string assemblyName, Version version)
    {
        var builder = new PersistedAssemblyBuilder(
            new AssemblyName(assemblyName) { Version = version },
            typeof(object).Assembly);
        var module = builder.DefineDynamicModule(assemblyName);
        module.DefineType(
                $"{assemblyName}.IWidget",
                TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract)
            .CreateType();
        builder.Save(path);
    }
}
