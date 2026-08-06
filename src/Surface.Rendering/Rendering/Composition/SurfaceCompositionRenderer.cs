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
    private readonly Func<string, bool> _blockIsAvailable;
    private readonly Func<string, IReadOnlySet<string>?> _confidentialControls;

    /// <param name="blockIsAvailable">
    /// Whether a block id still resolves to an installed, visible block. An orphan — its plugin
    /// uninstalled — is left out of the delivered page rather than rendered as a hole; the layout
    /// stays intact and becomes whole again when the plugin returns.
    /// </param>
    /// <param name="confidentialControls">Controls a block declared confidential, by block id.</param>
    public SurfaceCompositionRenderer(
        Func<string, bool>? blockIsAvailable = null,
        Func<string, IReadOnlySet<string>?>? confidentialControls = null)
    {
        _blockIsAvailable = blockIsAvailable ?? (_ => true);
        _confidentialControls = confidentialControls ?? (_ => null);
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
        markup.Append("<div class=\"cal-section\" data-cal-layout=\"")
            .Append(Attribute(section.Layout))
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
