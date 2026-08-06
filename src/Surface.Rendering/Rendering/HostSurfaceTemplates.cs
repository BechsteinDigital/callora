using System.Collections.Concurrent;
using System.Reflection;

namespace Callora.Surface.Rendering.Rendering;

/// <summary>
/// The host's own template bundle, addressed as <c>@callora/&lt;path&gt;</c>. It ships
/// inside this assembly rather than as published plugin assets, which buys three things:
/// it is available before any plugin is installed, it cannot be shadowed by a plugin that
/// claims the same id, and it needs no entry in a surface's chain — a template extending
/// the host it already runs in should not have to declare that as a dependency.
/// <para>
/// A relative path maps onto a manifest resource name by turning separators into dots,
/// so <c>layout/page.njk</c> reads <c>…Resources.views.surface.layout.page.njk</c>. Only
/// that direction is needed, so directory names containing dots would be ambiguous —
/// hence the convention that they do not.
/// </para>
/// </summary>
internal static class HostSurfaceTemplates
{
    /// <summary>The reserved bundle id. Matched case-insensitively, like every other.</summary>
    public const string BundleId = "callora";

    private const string ResourcePrefix = "Callora.Surface.Rendering.Resources.views.surface.";

    private static readonly Assembly Assembly = typeof(HostSurfaceTemplates).Assembly;

    // Manifest resources never change while the process runs, so a read is cached for
    // good. A miss is cached too: an SSR page that references a missing partial would
    // otherwise re-probe the manifest on every request.
    private static readonly ConcurrentDictionary<string, string?> Cache =
        new(StringComparer.Ordinal);

    public static bool Handles(string bundleId) =>
        string.Equals(bundleId, BundleId, StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads a template, or null when the path is malformed or names no resource.</summary>
    public static string? TryRead(string relativePath) =>
        string.IsNullOrWhiteSpace(relativePath) ? null : Cache.GetOrAdd(relativePath, Read);

    private static string? Read(string relativePath)
    {
        if (!IsWellFormed(relativePath))
        {
            return null;
        }

        var resourceName = ResourcePrefix + relativePath.Replace('/', '.');
        using var stream = Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // Traversal cannot actually escape here — the path becomes a lookup key in a fixed
    // set, not a filesystem walk — but a malformed name is rejected outright rather than
    // silently probing for a resource that happens to match.
    private static bool IsWellFormed(string relativePath)
    {
        if (relativePath[0] is '/' or '\\' || relativePath.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var segment in relativePath.Split('/'))
        {
            if (segment.Length == 0 || segment is "." or "..")
            {
                return false;
            }
        }

        return true;
    }
}
