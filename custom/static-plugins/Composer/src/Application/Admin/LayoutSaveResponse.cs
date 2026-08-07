namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>What an accepted autosave answers.</summary>
/// <param name="ChangedAtUtc">
/// The draft's new change stamp — what the editor must send with its next save.
/// <para>
/// Without it an editor could save exactly once. Its own stamp goes stale the moment the write
/// lands, so the next autosave would arrive with an outdated one and be refused as a conflict
/// against itself — the most confusing possible failure, because nobody else touched anything.
/// </para>
/// </param>
public sealed record LayoutSaveResponse(DateTimeOffset ChangedAtUtc);
