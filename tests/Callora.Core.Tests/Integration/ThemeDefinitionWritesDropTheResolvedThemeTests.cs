using Callora.Core.Application.Extensions;
using Callora.Core.Infrastructure.Persistence;
using Callora.Core.Tests.Support;
using Microsoft.EntityFrameworkCore;
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
[Collection(PostgresCollection.Name)]
public sealed class ThemeDefinitionWritesDropTheResolvedThemeTests(PostgresFixture postgres)
{

    // Eine Datenbank je TEST, nicht je Aufruf: xUnit erzeugt die Klasse für jede
    // Testmethode neu, also ist dieses Feld pro Test frisch. Ohne das bekäme jeder
    // Kontext innerhalb eines Tests eine eigene Datenbank — was ein Test, der zwei
    // gleichzeitige Verbindungen gegeneinander laufen lässt, sofort bemerkt: Der
    // Schreiber landet in der einen, die Leser suchen in der anderen.
    private string? _database;

    private async Task<string> DatabaseAsync() =>
        _database ??= await postgres.CreateDatabaseAsync();
    [SkippableFact]
    public async Task ReplacingDefinitions_DropsTheResolvedTheme()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");

        await using var context = new HostPersistenceDbContext(await OptionsAsync());
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
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");

        await using var context = new HostPersistenceDbContext(await OptionsAsync());
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
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");

        await using var context = new HostPersistenceDbContext(await OptionsAsync());
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

    private async Task<DbContextOptions<HostPersistenceDbContext>> OptionsAsync() =>
        new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(await DatabaseAsync())
            .Options;
}
