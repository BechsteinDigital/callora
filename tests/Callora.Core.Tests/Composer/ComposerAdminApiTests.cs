using System.Text.Json;
using Callora.Core.Application.Persistence.Contracts;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Composer.Application;
using Callora.Plugin.Composer.Infrastructure.Persistence;
using Callora.Plugin.Composer.Application.Admin;
using Xunit;

namespace Callora.Core.Tests.Composer;

/// <summary>
/// Die Admin-Fläche des Composers. Die wichtigste Aussage steht in den ersten beiden Tests: Der
/// Entwurf ist NUR über eine Route erreichbar, und die verlangt eine Berechtigung. Daran hängt die
/// Entwurfs-Garantie aus dem Core-Vertrag — zwei Methoden statt einer mit Schalter zu haben nützt
/// nichts, wenn die eine Methode ungeschützt im Netz steht.
/// </summary>
public sealed class ComposerAdminApiTests
{
    private static IReadOnlyList<HostAdminApiRouteRegistration> Routes() => Contributor().Routes;

    private static ComposerAdminApiExtensionContributor Contributor() => new(Store());

    // Die Routen-Tests fragen nach Berechtigungen, nicht nach Daten — der Store wird gebraucht,
    // aber nie benutzt. Eine Factory, die beim Zugriff wirft, sagt das deutlicher als ein Mock,
    // der still etwas zurückgäbe.
    private static SurfaceLayoutStore Store() => new(new UnusedFactory(), TimeProvider.System);

    private sealed class UnusedFactory : IPluginDbContextFactory<ComposerDbContext>
    {
        public ComposerDbContext CreateDbContext() =>
            throw new NotSupportedException("Dieser Test darf die Datenbank nicht anfassen.");

        public Task MigrateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public void EveryRouteDeclaresAPermission()
    {
        var unguarded = Routes()
            .Where(route => string.IsNullOrWhiteSpace(route.RequiredPermission))
            .Select(route => $"{route.HttpMethod} {route.RouteTemplate}")
            .ToArray();

        Assert.Empty(unguarded);
    }

    [Fact]
    public void ReadingADraftNeedsAPermission()
    {
        var draft = Routes().Single(route =>
            route.HttpMethod == "GET" && route.RouteTemplate.EndsWith("/draft", StringComparison.Ordinal));

        Assert.Equal(ComposerPermissionKeys.LayoutRead, draft.RequiredPermission);
    }

    [Fact]
    public void PublishingNeedsMoreThanWriting()
    {
        // Ein Entwurf ist noch niemandes Entscheidung — er darf falsch, halbfertig oder ein
        // Versuch sein. Veröffentlichen stellt ihn vor Besucher.
        var save = Routes().Single(route => route.HttpMethod == "PUT");
        var publish = Routes().Single(route =>
            route.RouteTemplate.EndsWith("/publish", StringComparison.Ordinal));

        Assert.Equal(ComposerPermissionKeys.LayoutWrite, save.RequiredPermission);
        Assert.Equal(ComposerPermissionKeys.LayoutPublish, publish.RequiredPermission);
        Assert.NotEqual(save.RequiredPermission, publish.RequiredPermission);
    }

    [Fact]
    public void DiscardingCountsAsPublishing()
    {
        // Beide entscheiden über den Unterschied zwischen dem, was jemand gebaut hat, und dem,
        // was Besucher sehen — nur in verschiedene Richtungen.
        var discard = Routes().Single(route =>
            route.RouteTemplate.EndsWith("/discard", StringComparison.Ordinal));

        Assert.Equal(ComposerPermissionKeys.LayoutPublish, discard.RequiredPermission);
    }

    [Fact]
    public void EveryRouteIsWorkspaceScoped()
    {
        // Ein Layout gehört einem Workspace. Global wäre die Ausnahme und müsste begründet
        // werden; hier gibt es keinen Grund.
        Assert.All(Routes(), route =>
            Assert.Equal(HostAdminApiRouteScope.Workspace, route.Scope));
    }

    [Fact]
    public void EveryDeclaredPermissionIsAnnounced()
    {
        // Sonst könnte eine Route eine Berechtigung verlangen, die im Katalog des Hosts nicht
        // existiert — und niemand könnte sie jemandem geben.
        var contributor = Contributor();

        Assert.All(contributor.Routes, route =>
            Assert.Contains(route.RequiredPermission, contributor.PermissionKeys));
    }

    [Fact]
    public void TheNavigationEntryNeedsTheReadPermission()
    {
        var item = Contributor().NavigationItems.Single();

        Assert.Equal(ComposerPermissionKeys.LayoutRead, item.RequiredPermission);
    }

    // ── Was die Handler mit schlechten Eingaben tun ─────────────────────────

    [Fact]
    public async Task SavingWithoutABodyIsRefused()
    {
        var response = await new LayoutSaveRouteHandler(Store())
            .HandleAsync(Request("PUT", body: null));

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task SavingWithAnUnreadableBodyIsRefused()
    {
        var response = await new LayoutSaveRouteHandler(Store())
            .HandleAsync(Request("PUT", body: JsonDocument.Parse("""{"document":5}""").RootElement));

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task ARouteWithoutItsLayoutKeyIsRefused()
    {
        var response = await new LayoutDraftRouteHandler(Store())
            .HandleAsync(new HostAdminApiRequest(
                "composer", "GET", "layouts//draft",
                new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, string[]>(StringComparer.Ordinal),
                null, "operator", "acme"));

        Assert.Equal(400, response.StatusCode);
    }

    private static HostAdminApiRequest Request(string method, JsonElement? body) => new(
        "composer",
        method,
        "layouts/portal/draft",
        new Dictionary<string, string>(StringComparer.Ordinal) { ["layoutKey"] = "portal" },
        new Dictionary<string, string[]>(StringComparer.Ordinal),
        body,
        "operator",
        "acme");
}
