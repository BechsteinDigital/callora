using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Surfaces;
using Callora.Surface.Rendering;
using Callora.Surface.Rendering.Rendering;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Composition rides on Nunjucks' own inheritance (#125 block C). A theme declares a
/// slot inside a block, so <c>extends</c>, <c>block</c> and <c>super()</c> keep
/// working and a child theme can wrap, move or replace a slot like any other markup.
/// </summary>
public sealed class SurfaceSlotRenderingTests
{
    [Fact]
    public void ASlotEmitsAnIslandPerResolvedView()
    {
        var html = Render(
            "<main>{{ callora_slot('workspace.main') }}</main>",
            Slot("workspace.main", View("comm.phone", "comm"), View("crm.lead-list", "crm")));

        Assert.Contains("data-callora-island=\"comm.phone\"", html, StringComparison.Ordinal);
        Assert.Contains("data-callora-island=\"crm.lead-list\"", html, StringComparison.Ordinal);
        Assert.Contains("data-callora-slot=\"workspace.main\"", html, StringComparison.Ordinal);
        Assert.Contains("data-callora-plugin=\"crm\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHostsOrderIsThePublishedOrder()
    {
        var html = Render(
            "{{ callora_slot('workspace.main') }}",
            Slot("workspace.main", View("comm.phone", "comm"), View("crm.lead-list", "crm")));

        Assert.True(
            html.IndexOf("comm.phone", StringComparison.Ordinal) <
            html.IndexOf("crm.lead-list", StringComparison.Ordinal));
    }

    [Fact]
    public void AnEmptySlotRendersNothingRatherThanFailing()
    {
        var html = Render("<main>{{ callora_slot('nobody.fills.this') }}</main>");

        Assert.Equal("<main></main>", html);
    }

    [Fact]
    public void ATemplateCanBranchOnWhetherASlotIsFilled()
    {
        const string template =
            "{% if callora_has_slot('workspace.main') %}filled{% else %}empty{% endif %}";

        Assert.Equal("empty", Render(template));
        Assert.Equal("filled", Render(template, Slot("workspace.main", View("crm.lead-list", "crm"))));
    }

    [Fact]
    public void CallSiteParametersTravelToTheIslandAsProps()
    {
        var html = Render(
            "{{ callora_slot('lead.detail.panel', { leadId: 42, mode: 'compact' }) }}",
            Slot("lead.detail.panel", View("crm.lead-panel", "crm")));

        // Attribute-escaped so the browser hands the runtime valid JSON back.
        Assert.Contains("data-callora-props=\"{&quot;leadId&quot;:42", html, StringComparison.Ordinal);
        Assert.Contains("&quot;mode&quot;:&quot;compact&quot;}\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ASingleViewCanBeEmbeddedByIdWithItsOwnParameters()
    {
        var html = Render(
            "{{ callora_view('vc.room', { roomId: 'r-7' }) }}",
            Slot("workspace.main", View("crm.lead-list", "crm"), View("vc.room", "videoconference")));

        Assert.Contains("data-callora-island=\"vc.room\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("crm.lead-list", html, StringComparison.Ordinal);
        Assert.Contains("r-7", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnfilledViewIdEmbedsNothing()
    {
        Assert.Equal(string.Empty, Render("{{ callora_view('nobody.registered.this') }}"));
    }

    [Fact]
    public void SlotMarkupIsNotDoubleEscapedByAutoescape()
    {
        var html = Render(
            "{{ callora_slot('workspace.main') }}",
            Slot("workspace.main", View("crm.lead-list", "crm")));

        // The globals return SafeString, so the emitted element stays an element.
        Assert.StartsWith("<div class=\"callora-island\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ATemplateCanIterateASlotToBuildItsOwnChrome()
    {
        var html = Render(
            "{% for view in callora_slot_views('workspace.main') %}[{{ view.displayName }}]{% endfor %}",
            Slot("workspace.main", View("crm.lead-list", "crm", "Leads")));

        Assert.Equal("[Leads]", html);
    }

    private static string Render(
        string template,
        params (string Slot, IReadOnlyList<SurfaceSlotView> Views)[] slots)
    {
        var context = new SurfaceRenderContext(
            "tenant-a",
            "workspace-a",
            "portal",
            "spa",
            "de",
            new Dictionary<string, string>(StringComparer.Ordinal))
        {
            Slots = slots.ToDictionary(x => x.Slot, x => x.Views, StringComparer.Ordinal),
        };

        return new NunjucksSurfaceRenderer().Render(template, context);
    }

    private static (string Slot, IReadOnlyList<SurfaceSlotView> Views) Slot(
        string slot,
        params SurfaceSlotView[] views) =>
        (slot, views);

    private static SurfaceSlotView View(string viewId, string pluginId, string displayName = "View") =>
        new(viewId, pluginId, "workspace.main", displayName, 0, SurfaceViewCardinality.Multiple, null, [], []);
}
