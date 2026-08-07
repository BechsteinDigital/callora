using System.Text.Json;

namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>What an autosave sends.</summary>
/// <param name="Document">The layout document as the editor has it.</param>
/// <param name="ExpectedChangedAtUtc">
/// The stamp the editor last read. A save whose stamp no longer matches is refused — the editor
/// composed it against a state that somebody else has since replaced.
/// </param>
public sealed record LayoutSaveRequest(JsonElement Document, DateTimeOffset ExpectedChangedAtUtc);
