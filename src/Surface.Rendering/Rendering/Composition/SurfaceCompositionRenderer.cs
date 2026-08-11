using System.Text;
using System.Text.Encodings.Web;
using Callora.Core.Application.Surfaces.Layout;

namespace Callora.Surface.Rendering.Rendering.Composition;

/// <summary>
/// Renders a composed layout to markup: one container per section, one island per block.
/// <para>
/// The island format is the one <c>mount.ts</c> already understands, so the surface runtime needs
/// no change — this renderer is purely additive. A page built in the editor and a page built from
/// a template hydrate through the same path.
/// </para>
/// <para>
/// <b>A block has exactly one rendering — its Vue component.</b> A second, server-side path (a
/// <c>.njk</c> partial per block) was considered and rejected: two implementations of the same
/// appearance drift, and in a direct-manipulation canvas every config change would need a server
/// round trip. The consequence is that editor-built pages are islands without SSR content. For
/// SEO landing pages the template route stays open; for workplaces and portals — the point of
/// this — it does not matter.
/// </para>
/// </summary>
public sealed class SurfaceCompositionRenderer
{
    /// <summary>
    /// What a section falls back to when its theme no longer knows the declared layout. Not a
    /// theme-declared value: a theme that dropped `single` too has nothing left to fall back to,
    /// and the point of the fallback is that the CONTENT survives a theme change, not that it
    /// still looks right.
    /// </summary>
    public const string FallbackLayout = "single";

    private readonly Func<string, bool> _blockIsAvailable;
    private readonly Func<string, IReadOnlySet<string>?> _confidentialControls;
    private readonly Func<string, bool> _layoutIsKnown;

    /// <param name="blockIsAvailable">
    /// Whether a block id still resolves to an installed, visible block. An orphan — its plugin
    /// uninstalled — is left out of the delivered page rather than rendered as a hole; the layout
    /// stays intact and becomes whole again when the plugin returns.
    /// </param>
    /// <param name="confidentialControls">
    /// Controls a block declared confidential, by block id.
    /// <para>
    /// <b>Heute unversorgt, und das ist die ehrliche Antwort.</b> <c>confidential</c> steht im
    /// Browser-Vertrag der Blöcke (<c>block-contract.ts</c>); serverseitig gibt es keine
    /// Blockbeschreibung, aus der der Host es lesen könnte. Der Parameter ist deshalb der
    /// vorbereitete Anschluss, nicht eine Filterung, die nur gerade niemand benutzt — wer eine
    /// solche Quelle baut, verdrahtet sie hier, und bis dahin sagt der Renderpfad nicht zu, was
    /// er nicht halten kann (siehe <c>page/composed.njk</c>).
    /// </para>
    /// </param>
    /// <param name="layoutIsKnown">
    /// Whether the active theme still declares this section layout. It stops declaring one when
    /// somebody switches themes — and then <c>data-cal-layout="two-2-1"</c> would name a grid
    /// nothing styles, so the section would collapse into whatever the browser does with
    /// unstyled divs. Falling back to <see cref="FallbackLayout"/> keeps the blocks in one
    /// readable column instead (§7.8): the page looks plainer, and nothing is lost.
    /// </param>
    public SurfaceCompositionRenderer(
        Func<string, bool>? blockIsAvailable = null,
        Func<string, IReadOnlySet<string>?>? confidentialControls = null,
        Func<string, bool>? layoutIsKnown = null)
    {
        _blockIsAvailable = blockIsAvailable ?? (_ => true);
        _confidentialControls = confidentialControls ?? (_ => null);
        _layoutIsKnown = layoutIsKnown ?? (_ => true);
    }

    /// <summary>Renders the document. Sections and blocks come out in their declared order.</summary>
    public string Render(SurfaceLayoutDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var markup = new StringBuilder();
        foreach (var section in document.Sections.OrderBy(s => s.Position))
        {
            RenderSection(markup, section);
        }

        return markup.ToString();
    }

    private void RenderSection(StringBuilder markup, SurfaceLayoutSection section)
    {
        // Ein Layout, das das Theme nicht mehr kennt, wird ersetzt statt ausgeliefert. Der
        // Inhalt bleibt vollständig; er steht nur einspaltig, bis das Theme das Layout wieder
        // mitbringt.
        var layout = _layoutIsKnown(section.Layout) ? section.Layout : FallbackLayout;

        markup.Append("<div class=\"cal-section\" data-cal-layout=\"")
            .Append(Attribute(layout))
            .Append('"');

        // Token STEPS, not values — the attribute names a step the theme resolves, so a section
        // can be roomier without anyone being able to write a pixel here.
        if (!string.IsNullOrWhiteSpace(section.Spacing))
        {
            markup.Append(" data-cal-spacing=\"").Append(Attribute(section.Spacing)).Append('"');
        }

        if (!string.IsNullOrWhiteSpace(section.SurfaceRole))
        {
            markup.Append(" data-cal-surface=\"").Append(Attribute(section.SurfaceRole)).Append('"');
        }

        markup.Append('>');

        foreach (var group in section.Blocks.GroupBy(b => b.Region).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            markup.Append("<div class=\"cal-region\" data-cal-region=\"")
                .Append(Attribute(group.Key))
                .Append("\">");

            foreach (var block in group.OrderBy(b => b.Position))
            {
                RenderBlock(markup, block);
            }

            markup.Append("</div>");
        }

        markup.Append("</div>");
    }

    private void RenderBlock(StringBuilder markup, SurfaceLayoutBlock block)
    {
        if (!_blockIsAvailable(block.BlockId))
        {
            return;
        }

        markup.Append("<div class=\"callora-island\" data-callora-island=\"")
            .Append(Attribute(block.BlockId))
            .Append('"');

        if (SurfaceBlockPropsSerializer.Serialize(block, _confidentialControls(block.BlockId)) is { } props)
        {
            markup.Append(" data-callora-props=\"").Append(Attribute(props)).Append('"');
        }

        markup.Append("></div>");
    }

    // Everything that reaches an attribute is encoded, including values that came from a
    // configured layout: an operator is trusted, a stored string is not.
    private static string Attribute(string? value) =>
        HtmlEncoder.Default.Encode(value ?? string.Empty);
}
