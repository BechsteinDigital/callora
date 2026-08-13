using Callora.Core.Application.Configuration;
using Callora.Core.Tests.Support;
using Xunit;

namespace Callora.Core.Tests.Application.Configuration;

public sealed class SystemConfigResolverTests
{
    [Fact]
    public async Task Resolve_UsesDefinitionDefault_WhenNoValueStored()
    {
        var store = new InMemorySystemConfigStore();
        await store.ReplaceDefinitionsForPluginAsync("voip", "1.0.0",
            [new SystemConfigDefinitionInput("codec.preferred", "Codec", "text", null, "\"PCMA\"", null, null, 10, true)]);
        var resolver = new SystemConfigResolver(store);

        var effective = await resolver.ResolveAsync("voip", workspaceKey: "test");

        Assert.Equal("\"PCMA\"", effective["codec.preferred"]);
    }

    [Fact]
    public async Task Resolve_WorkspaceValue_OverridesTenantGlobalAndDefault()
    {
        var store = new InMemorySystemConfigStore();
        await store.ReplaceDefinitionsForPluginAsync("voip", "1.0.0",
            [new SystemConfigDefinitionInput("codec.preferred", "Codec", "text", null, "\"PCMA\"", null, null, 10, true)]);
        await store.UpsertValuesAsync("voip", SystemConfigScopes.Global, "", new Dictionary<string, string?> { ["codec.preferred"] = "\"PCMU\"" });
        await store.UpsertValuesAsync("voip", SystemConfigScopes.Tenant, "tenant-a", new Dictionary<string, string?> { ["codec.preferred"] = "\"G722\"" });
        await store.UpsertValuesAsync("voip", SystemConfigScopes.Workspace, "workspace-a", new Dictionary<string, string?> { ["codec.preferred"] = "\"opus\"" });
        var resolver = new SystemConfigResolver(store);

        Assert.Equal("\"opus\"", await resolver.ResolveValueAsync("voip", "codec.preferred", "tenant-a", "workspace-a"));
        Assert.Equal("\"G722\"", await resolver.ResolveValueAsync("voip", "codec.preferred", "tenant-a"));
        Assert.Equal("\"PCMU\"", await resolver.ResolveValueAsync("voip", "codec.preferred"));
    }

    [Fact]
    public async Task Resolve_IgnoresInactiveDefinitions()
    {
        var store = new InMemorySystemConfigStore();
        await store.ReplaceDefinitionsForPluginAsync("voip", "1.0.0",
            [new SystemConfigDefinitionInput("legacy.flag", "Legacy", "bool", null, "true", null, null, 10, false)]);
        var resolver = new SystemConfigResolver(store);

        var effective = await resolver.ResolveAsync("voip");

        Assert.False(effective.ContainsKey("legacy.flag"));
    }

    [Fact]
    public async Task DeletingValue_FallsBackToLessSpecificScope()
    {
        var store = new InMemorySystemConfigStore();
        await store.ReplaceDefinitionsForPluginAsync("voip", "1.0.0",
            [new SystemConfigDefinitionInput("codec.preferred", "Codec", "text", null, "\"PCMA\"", null, null, 10, true)]);
        await store.UpsertValuesAsync("voip", SystemConfigScopes.Workspace, "workspace-a", new Dictionary<string, string?> { ["codec.preferred"] = "\"opus\"" });
        await store.UpsertValuesAsync("voip", SystemConfigScopes.Workspace, "workspace-a", new Dictionary<string, string?> { ["codec.preferred"] = null });
        var resolver = new SystemConfigResolver(store);

        Assert.Equal("\"PCMA\"", await resolver.ResolveValueAsync("voip", "codec.preferred", workspaceKey: "workspace-a"));
    }

    /// <summary>
    /// Zwei Workspaces, die sich nur in der Schreibweise unterscheiden, sind zwei Workspaces — der
    /// Workspace-Store trimmt beim Anlegen und schreibt sonst nichts klein. Der Lesepfad der
    /// Konfiguration war der einzige Beteiligte, der Groß- und Kleinschreibung ignorierte: Der
    /// Unique-Index tut es nicht, der Schreibpfad tut es nicht. Damit sahen "Acme" und "acme"
    /// gegenseitig ihre Werte, und welcher gewann, hing an der Zeilenreihenfolge.
    /// </summary>
    [Fact]
    public async Task WorkspacesThatDifferOnlyInCasingDoNotSeeEachOthersValues()
    {
        var store = new InMemorySystemConfigStore();
        await store.ReplaceDefinitionsForPluginAsync("voip", "1.0.0",
            [new SystemConfigDefinitionInput("codec.preferred", "Codec", "text", null, "\"PCMA\"", null, null, 10, true)]);
        await store.UpsertValuesAsync("voip", SystemConfigScopes.Workspace, "Acme", new Dictionary<string, string?> { ["codec.preferred"] = "\"opus\"" });
        var resolver = new SystemConfigResolver(store);

        Assert.Equal("\"opus\"", await resolver.ResolveValueAsync("voip", "codec.preferred", workspaceKey: "Acme"));

        // Der andere Workspace fällt auf den Vorgabewert zurück, statt den fremden Wert zu erben.
        Assert.Equal("\"PCMA\"", await resolver.ResolveValueAsync("voip", "codec.preferred", workspaceKey: "acme"));
    }
}
