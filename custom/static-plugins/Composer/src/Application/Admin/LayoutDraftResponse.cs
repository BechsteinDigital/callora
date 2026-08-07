using System.Text.Json;
using Callora.Plugin.Composer.Domain;

namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>
/// The draft as the editor receives it.
/// </summary>
/// <param name="LayoutKey">Which layout.</param>
/// <param name="WorkspaceKey">
/// Which workspace the layout belongs to. Sent even though the session is workspace-bound: a
/// platform operator can address another workspace through <c>?workspaceKey=</c>, and the layout's
/// own workspace is the one whose plugin chain decides which blocks this layout can hold.
/// </param>
/// <param name="SurfaceKey">
/// Which surface the layout renders, or null when it is not bound to one yet. The editor loads
/// that surface's block bundles — a layout bound to a kiosk surface must not be composed from the
/// blocks of the default one, or the canvas would offer blocks that are not there once it is live.
/// </param>
/// <param name="VersionNumber">The number this draft would carry if published.</param>
/// <param name="Document">The layout document.</param>
/// <param name="ChangedAtUtc">
/// The stamp to send back when saving. Without it a save cannot be checked against what it was
/// composed from, and the second writer would silently win.
/// </param>
public sealed record LayoutDraftResponse(
    string LayoutKey,
    string WorkspaceKey,
    string? SurfaceKey,
    int VersionNumber,
    JsonElement Document,
    DateTimeOffset ChangedAtUtc)
{
    /// <summary>
    /// Composes the response from the layout's identity and its draft.
    /// <para>
    /// An unbound layout keeps a null <see cref="SurfaceKey"/> rather than falling back to the
    /// default surface. Substituting one here would read, on the wire, exactly like a layout that
    /// IS bound to the default surface — and the editor would silently compose against the wrong
    /// set of blocks instead of saying that nobody has decided where this layout goes.
    /// </para>
    /// </summary>
    public static LayoutDraftResponse For(SurfaceLayout layout, SurfaceLayoutVersion draft)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(draft);

        return new LayoutDraftResponse(
            draft.LayoutKey,
            layout.WorkspaceKey,
            layout.SurfaceKey,
            draft.VersionNumber,
            JsonSerializer.Deserialize<JsonElement>(draft.Document),
            draft.ChangedAtUtc);
    }
}
