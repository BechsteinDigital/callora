using System.Text.Json;

namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>
/// The draft as the editor receives it.
/// </summary>
/// <param name="LayoutKey">Which layout.</param>
/// <param name="VersionNumber">The number this draft would carry if published.</param>
/// <param name="Document">The layout document.</param>
/// <param name="ChangedAtUtc">
/// The stamp to send back when saving. Without it a save cannot be checked against what it was
/// composed from, and the second writer would silently win.
/// </param>
public sealed record LayoutDraftResponse(
    string LayoutKey,
    int VersionNumber,
    JsonElement Document,
    DateTimeOffset ChangedAtUtc);
