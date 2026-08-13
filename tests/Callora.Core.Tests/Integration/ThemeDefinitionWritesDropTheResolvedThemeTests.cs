using Callora.Core.Application.Extensions;
using Callora.Core.Infrastructure.Persistence;
using Callora.Core.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Callora.Core.Tests.Integration;

/// <summary>
/// Die beiden Theme-Schreibwege, die sich mit dem In-Memory-Provider nicht prüfen lassen:
/// <c>ReplaceDefinitionsForPluginAsync</c> und <c>ClearPluginDefinitionsAsync</c>. Beide benutzen
/// <c>ExecuteDelete</c> in einer Transaktion, und der Fake kann weder das eine noch das andere.
/// <para>
/// Sie hier auszulassen wäre bequem gewesen — beide laufen nur beim Installieren und
/// Deinstallieren eines Theme-Plugins, also selten. Aber genau dort schlägt ein vergessener
/// Aufruf am unangenehmsten zu: Ein frisch installiertes Theme brächte seine Definitionen mit,
/// und der Renderpfad lieferte weiter die Werte des alten, bis die Rückfallzeit abliefe. „Selten"
/// ist kein Grund, es nicht zu wissen.
/// </para>
/// </summary>
[Trait("Category", "Slow")]
public sealed class ThemeDefinitionWritesDropTheResolvedThemeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private bool _started;

    public async Task InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();
            _started = true;
        }
        catch (Exception)
        {
            _started = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_started)
        {
            await _postgres.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task ReplacingDefinitions_DropsTheResolvedTheme()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");

        await using var context = new HostPersistenceDbContext(Options());
        await context.Database.EnsureCreatedAsync();
        var themeCache = new CountingThemeResolutionCache();
        var store = new EfWorkspaceThemeSettingsStore(context, themeCache);

        await store.ReplaceDefinitionsForPluginAsync(
            "theme-plugin",
            "1.0.0",
            [Definition("primary-color", "\"#000000\"")]);

        Assert.True(
            themeCache.InvalidationCount > 0,
            "Neue Definitionen ändern, was der Renderpfad ausliefert — das gecachte Theme ist danach falsch.");
    }

    /// <summary>
    /// Der zweite Weg, und der mit der klareren Folge: Nach dem Deinstallieren gibt es die
    /// Definitionen nicht mehr. Bliebe das aufgelöste Theme stehen, lieferte die Fläche Werte
    /// eines Themes aus, das nicht mehr installiert ist.
    /// </summary>
    [SkippableFact]
    public async Task ClearingPluginDefinitions_DropsTheResolvedTheme()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");

        await using var context = new HostPersistenceDbContext(Options());
        await context.Database.EnsureCreatedAsync();
        var themeCache = new CountingThemeResolutionCache();
        var store = new EfWorkspaceThemeSettingsStore(context, themeCache);

        await store.ReplaceDefinitionsForPluginAsync(
            "theme-plugin",
            "1.0.0",
            [Definition("primary-color", "\"#000000\"")]);
        var afterInstall = themeCache.InvalidationCount;

        await store.ClearPluginDefinitionsAsync("theme-plugin");

        Assert.True(
            themeCache.InvalidationCount > afterInstall,
            "Nach dem Deinstallieren darf kein aufgelöstes Theme dieses Plugins mehr ausgeliefert werden.");
    }

    /// <summary>
    /// Die Gegenprobe zum In-Memory-Test: Hier läuft die echte Transaktion mit ExecuteDelete
    /// durch, und die Werte verschwinden tatsächlich. Ohne sie wäre oben nur belegt, dass
    /// jemand einen Zähler hochzählt.
    /// </summary>
    [SkippableFact]
    public async Task ClearingPluginDefinitions_AlsoRemovesTheValues()
    {
        Skip.IfNot(_started, "Docker/Postgres container not available.");

        await using var context = new HostPersistenceDbContext(Options());
        await context.Database.EnsureCreatedAsync();
        var store = new EfWorkspaceThemeSettingsStore(context, new CountingThemeResolutionCache());

        await store.ReplaceDefinitionsForPluginAsync(
            "theme-plugin",
            "1.0.0",
            [Definition("primary-color", "\"#000000\"")]);
        await store.ReplaceValuesAsync(
            "acme",
            surfaceKey: null,
            "theme-plugin",
            new Dictionary<string, string?> { ["primary-color"] = "\"#ff0000\"" });

        await store.ClearPluginDefinitionsAsync("theme-plugin");

        var definitions = await store.ListDefinitionsAsync("theme-plugin", "1.0.0");
        var values = await store.ListValuesAsync("acme", surfaceKey: null, "theme-plugin");
        Assert.Empty(definitions);
        Assert.Empty(values);
    }

    private static WorkspaceThemeSettingDefinitionInput Definition(string key, string defaultValueJson) =>
        new(
            SettingKey: key,
            Label: key,
            FieldType: "color",
            Description: null,
            DefaultValueJson: defaultValueJson,
            IsRequired: false,
            SortOrder: 10,
            GroupName: null,
            OptionsJson: null,
            IsActive: true);

    private DbContextOptions<HostPersistenceDbContext> Options() =>
        new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
}
