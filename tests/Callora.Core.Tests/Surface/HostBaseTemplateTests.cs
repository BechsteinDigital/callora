using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Surfaces;
using Callora.Surface.Rendering;
using Callora.Surface.Rendering.Rendering;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// What the shipped base templates actually render. These exercise the path a plugin
/// author takes — extend a layout, fill a block — rather than the loader underneath it
/// (<see cref="HostBaseBundleTests"/>).
/// </summary>
[Collection(SurfaceRenderingCollection.Name)]
public sealed class HostBaseTemplateTests
{
    [Fact]
    public void PageLayout_PutsTheTitleInBothTheDocumentAndTheHeading()
    {
        var html = Render(
            """
            {% extends "@callora/layout/page.njk" %}
            {% set page_title = "Kunden" %}
            {% block page_content %}<p>Inhalt</p>{% endblock %}
            """);

        Assert.Contains("<title>Kunden</title>", html, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"cal-page__title\">Kunden</h1>", html, StringComparison.Ordinal);
        Assert.Contains("<p>Inhalt</p>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void PageLayout_WithoutATitle_RendersNoHeading()
    {
        // A landing page has no heading of its own; an empty <h1> would be worse than none.
        var html = Render("""{% extends "@callora/layout/page.njk" %}""");

        Assert.DoesNotContain("cal-page__header", html, StringComparison.Ordinal);
        Assert.Contains("<title>portal</title>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void BlankLayout_RemovesNavigationRatherThanHidingIt()
    {
        var html = Render(
            """{% extends "@callora/layout/blank.njk" %}{% block page_content %}Login{% endblock %}""",
            navigation: [new SurfaceNavigationEntry("n1", "crm", "Kunden", "/kunden", null, 10)]);

        Assert.DoesNotContain("cal-nav", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Kunden", html, StringComparison.Ordinal);
        Assert.DoesNotContain("cal-header", html, StringComparison.Ordinal);
        Assert.Contains("Login", html, StringComparison.Ordinal);
    }

    [Fact]
    public void SidebarLayout_PutsMainBeforeTheRailInTheSource()
    {
        var html = Render(
            """
            {% extends "@callora/layout/sidebar.njk" %}
            {% block page_content %}HAUPT{% endblock %}
            {% block sidebar_content %}RAIL{% endblock %}
            """);

        // Erst die Anwesenheit: IndexOf liefert -1 für einen fehlenden String, und -1
        // ist kleiner als alles — ein reiner Reihenfolgevergleich wäre auch grün, wenn
        // der Inhalt gar nicht gerendert würde.
        Assert.Contains("HAUPT", html, StringComparison.Ordinal);
        Assert.Contains("RAIL", html, StringComparison.Ordinal);
        Assert.True(
            html.IndexOf("HAUPT", StringComparison.Ordinal) < html.IndexOf("RAIL", StringComparison.Ordinal),
            "Der Inhalt muss vor dem Nebenbereich stehen — Lesereihenfolge, nicht Darstellung.");
        Assert.Contains("cal-surface--sidebar", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Navigation_RendersWhatThePluginsContributed_InTheHostsOrder()
    {
        var html = Render(
            """{% extends "@callora/layout/page.njk" %}""",
            navigation:
            [
                new SurfaceNavigationEntry("n1", "crm", "Kunden", "/kunden", null, 10),
                new SurfaceNavigationEntry("n2", "comm", "Anrufe", "/anrufe", "phone", 20),
            ]);

        Assert.Contains("Kunden", html, StringComparison.Ordinal);
        Assert.Contains("Anrufe", html, StringComparison.Ordinal);
        Assert.True(
            html.IndexOf("Kunden", StringComparison.Ordinal) < html.IndexOf("Anrufe", StringComparison.Ordinal));
        Assert.Contains("data-icon=\"phone\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Navigation_WithNoEntries_EmitsNoLandmark()
    {
        // An empty <nav> is announced as a navigation landmark that leads nowhere.
        var html = Render("""{% extends "@callora/layout/page.njk" %}""");

        Assert.DoesNotContain("<nav", html, StringComparison.Ordinal);
    }

    [Fact]
    public void IndexPage_WithNothingContributed_SaysSoRatherThanRenderingBlank()
    {
        var html = Render("""{% extends "@callora/page/index.njk" %}""");

        Assert.Contains("Diese Fläche ist noch leer", html, StringComparison.Ordinal);
    }

    [Fact]
    public void IndexPage_WithAContribution_RendersTheIslandAndNoEmptyState()
    {
        var html = Render(
            """{% extends "@callora/page/index.njk" %}""",
            slots: new Dictionary<string, IReadOnlyList<SurfaceSlotView>>(StringComparer.Ordinal)
            {
                ["surface.main"] = [View("crm.lead-list", "crm")],
            });

        Assert.Contains("data-callora-island=\"crm.lead-list\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Diese Fläche ist noch leer", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Base_CarriesTheRuntimeContextSoIslandsCanHydrate()
    {
        // mount.ts falls back to [data-workspace] when there is no #callora-app root;
        // without these attributes hydration silently uses defaults.
        var html = Render("""{% extends "@callora/layout/page.njk" %}""");

        Assert.Contains("data-workspace=\"workspace-a\"", html, StringComparison.Ordinal);
        Assert.Contains("data-surface=\"portal\"", html, StringComparison.Ordinal);
        Assert.Contains("data-caller-state=\"anonymous\"", html, StringComparison.Ordinal);
        Assert.Contains("/surface-app/surface.js", html, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDefaultShell_CarriesBothContributionPaths()
    {
        // Was der Host rendert, wenn kein Plugin ein SSR-Entry veröffentlicht. Beide Wege
        // müssen offen bleiben: serverseitig aufgelöste Views als Inseln, clientseitig
        // registrierte über den App-Root. mountSurface bedient beide in einem Durchlauf.
        var html = Render(
            SurfaceShellTemplates.SpaRoot,
            slots: new Dictionary<string, IReadOnlyList<SurfaceSlotView>>(StringComparer.Ordinal)
            {
                ["surface.main"] = [View("crm.lead-list", "crm")],
            });

        Assert.Contains("id=\"callora-app\"", html, StringComparison.Ordinal);
        Assert.Contains("data-callora-island=\"crm.lead-list\"", html, StringComparison.Ordinal);
        // Und die Fläche sieht aus wie eine Fläche, nicht wie ein leeres div.
        Assert.Contains("cal-header", html, StringComparison.Ordinal);
        Assert.Contains("/surface-base/base.css", html, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAppRoot_CarriesTheContextOnItself()
    {
        // readSurfaceContext liest den EIGENEN Datensatz des App-Roots, nicht den eines
        // Vorfahren — stünden die Attribute nur am Body, mountete die App mit Defaults.
        var html = Render(SurfaceShellTemplates.SpaRoot);

        var root = html.IndexOf("id=\"callora-app\"", StringComparison.Ordinal);
        Assert.True(root > 0, "Kein App-Root gerendert.");
        var tagEnd = html.IndexOf('>', root);
        Assert.Contains("data-workspace=\"workspace-a\"", html[root..tagEnd], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("surface.head")]
    [InlineData("surface.overlay")]
    [InlineData("surface.body.end")]
    public void MoreThanOnePluginCanContributeToTheSameRegion(string slot)
    {
        // Der Grund, warum wir kein sw_extends brauchen: Wo mehrere beitragen wollen,
        // steht ein Slot statt eines Blocks. Ein Block gehört genau einem Template —
        // zwei Plugins, die etwas in den <head> wollen, würden einander verdrängen.
        var html = Render(
            """{% extends "@callora/layout/page.njk" %}""",
            slots: new Dictionary<string, IReadOnlyList<SurfaceSlotView>>(StringComparer.Ordinal)
            {
                [slot] = [View("a.beitrag", "plugin-a"), View("b.beitrag", "plugin-b")],
            });

        Assert.Contains("data-callora-island=\"a.beitrag\"", html, StringComparison.Ordinal);
        Assert.Contains("data-callora-island=\"b.beitrag\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Base_KeepsTheSkipLinkFirstInTheTabOrder()
    {
        var html = Render("""{% extends "@callora/layout/page.njk" %}""");

        var skip = html.IndexOf("cal-skip-link", StringComparison.Ordinal);
        Assert.True(skip > 0, "Der Skip-Link fehlt.");
        Assert.True(
            skip < html.IndexOf("cal-header", StringComparison.Ordinal),
            "Der Skip-Link muss vor dem Header stehen, sonst überspringt er nichts.");
    }

    [Fact]
    public void ATemplateCanReplaceAWholeRegionWithoutTouchingTheRest()
    {
        var html = Render(
            """
            {% extends "@callora/layout/page.njk" %}
            {% block base_header %}<header id="mein-kopf">X</header>{% endblock %}
            """);

        Assert.Contains("mein-kopf", html, StringComparison.Ordinal);
        Assert.DoesNotContain("cal-header__brand", html, StringComparison.Ordinal);
        // Der Rest bleibt stehen.
        Assert.Contains("cal-footer", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Card_TakesItsHeadingLevelFromTheCaller()
    {
        var html = Render(
            """
            {% extends "@callora/layout/page.njk" %}
            {% from "@callora/component/card.njk" import card %}
            {% block page_content %}{% call card("Letzte Anrufe", 3) %}<p>…</p>{% endcall %}{% endblock %}
            """);

        Assert.Contains("<h3 class=\"cal-card__title\">Letzte Anrufe</h3>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorPage_SaysNothingAboutWhatWentWrongInternally()
    {
        var html = Render(
            """
            {% extends "@callora/page/error.njk" %}
            {% set error_title = "Nicht gefunden" %}
            """);

        Assert.Contains("Nicht gefunden", html, StringComparison.Ordinal);
        Assert.DoesNotContain("cal-nav", html, StringComparison.Ordinal);
    }

    private static string Render(
        string template,
        IReadOnlyList<SurfaceNavigationEntry>? navigation = null,
        IReadOnlyDictionary<string, IReadOnlyList<SurfaceSlotView>>? slots = null)
    {
        var context = new SurfaceRenderContext(
            "tenant-a",
            "workspace-a",
            "portal",
            "spa",
            "de",
            new Dictionary<string, string>(StringComparer.Ordinal))
        {
            Navigation = navigation ?? [],
            Slots = slots ?? new Dictionary<string, IReadOnlyList<SurfaceSlotView>>(StringComparer.Ordinal),
        };

        return new NunjucksSurfaceRenderer().Render(template, context, []);
    }

    private static SurfaceSlotView View(string viewId, string pluginId) => new(
        viewId, pluginId, "surface.main", "View", 0, SurfaceViewCardinality.Multiple, null, [], []);
}
