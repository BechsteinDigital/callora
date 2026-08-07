using Callora.Core.Application.Extensions;
using System.Text.Json;

namespace Callora.Core.Infrastructure.Extensions;

/// <summary>
/// Reads the section layouts out of a <c>theme.json</c>.
/// <para>
/// A pure parser: it takes the parsed document and returns what it found. Nothing here touches
/// disk or database, so every rule below — what makes a layout valid, what a missing label falls
/// back to, which malformed entry is skipped rather than fatal — can be stated as a test against
/// a JSON string.
/// </para>
/// <para>
/// <b>Skipping beats throwing.</b> A theme with one malformed layout still offers its others; a
/// parser that gave up would leave the editor with no layouts at all, which looks exactly like a
/// theme that declares none, and nobody would know where to look.
/// </para>
/// </summary>
public static class SectionLayoutManifestReader
{
    /// <summary>
    /// Whether this manifest inherits the base theme's section layouts.
    /// <para>
    /// Default true, and that is the safe direction: the runtime's base stylesheet is always
    /// loaded, so the base layouts work under a foreign theme too, and a theme that only wants
    /// to add <c>sidebar-right</c> need not repeat the whole palette. Setting it false means
    /// standing alone for everything the theme's own layouts need.
    /// </para>
    /// </summary>
    public static bool ParseInheritsBase(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        return root.TryGetProperty("inheritSectionLayouts", out var found) &&
               found.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? found.GetBoolean()
            : true;
    }

    /// <summary>
    /// The declared layouts, in file order. Empty when the manifest declares none — which is a
    /// valid state: a theme that only styles tokens has no layouts to offer, and inherits the
    /// base ones.
    /// </summary>
    public static IReadOnlyList<SectionLayoutDefinition> Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        if (!TryGetArray(root, "sectionLayouts", out var container) &&
            !TryGetArray(root, "layouts", out container))
        {
            return [];
        }

        var layouts = new List<SectionLayoutDefinition>();
        var order = 0;
        foreach (var item in container.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var layoutKey = FirstNonEmpty(TryGetString(item, "key"), TryGetString(item, "layoutKey"));
            if (string.IsNullOrWhiteSpace(layoutKey))
            {
                // Ohne Schlüssel gibt es nichts, worauf das Theme-CSS zielen könnte, und nichts,
                // was in ein Dokument geschrieben werden dürfte.
                continue;
            }

            layouts.Add(new SectionLayoutDefinition(
                layoutKey.Trim(),
                FirstNonEmpty(TryGetString(item, "label"), TryGetString(item, "name"), layoutKey).Trim(),
                ParseRegions(item),
                TryGetInt32(item, "sortOrder") ?? TryGetInt32(item, "order") ?? (order += 10)));
        }

        return layouts;
    }

    /// <summary>
    /// The regions of one layout, in declared order — the theme's order is the reading order.
    /// Sorting them would put a sidebar before the content it sits next to.
    /// </summary>
    private static IReadOnlyList<SectionLayoutRegion> ParseRegions(JsonElement layout)
    {
        if (!TryGetArray(layout, "regions", out var container))
        {
            return [];
        }

        var regions = new List<SectionLayoutRegion>();
        foreach (var item in container.EnumerateArray())
        {
            // Eine Region darf auch nur ihr Schlüssel sein: `"regions": ["main", "aside"]` ist
            // die Form, in der ein Theme-Autor sie zuerst hinschreibt.
            if (item.ValueKind == JsonValueKind.String)
            {
                var shorthand = item.GetString();
                if (!string.IsNullOrWhiteSpace(shorthand))
                {
                    regions.Add(new SectionLayoutRegion(shorthand.Trim(), shorthand.Trim()));
                }

                continue;
            }

            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var regionKey = FirstNonEmpty(TryGetString(item, "key"), TryGetString(item, "regionKey"));
            if (string.IsNullOrWhiteSpace(regionKey))
            {
                continue;
            }

            regions.Add(new SectionLayoutRegion(
                regionKey.Trim(),
                FirstNonEmpty(TryGetString(item, "label"), TryGetString(item, "name"), regionKey).Trim()));
        }

        return regions;
    }

    private static bool TryGetArray(JsonElement element, string name, out JsonElement array)
    {
        if (element.TryGetProperty(name, out var found) && found.ValueKind == JsonValueKind.Array)
        {
            array = found;
            return true;
        }

        array = default;
        return false;
    }

    private static string? TryGetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var found) && found.ValueKind == JsonValueKind.String
            ? found.GetString()
            : null;

    private static int? TryGetInt32(JsonElement element, string name) =>
        element.TryGetProperty(name, out var found) && found.ValueKind == JsonValueKind.Number &&
        found.TryGetInt32(out var value)
            ? value
            : null;

    private static string FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate)) ?? string.Empty;
}
