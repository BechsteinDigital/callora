namespace Callora.Administration.Api.Admin.Diagnostics;

/// <summary>State of the recorder after a start or stop.</summary>
/// <param name="IsRecording">Whether recording is on.</param>
/// <param name="WindowSeconds">The window actually in effect, after clamping.</param>
/// <param name="PluginId">The plugin being recorded, or null for all.</param>
/// <param name="CapturedCommands">How many commands are currently held.</param>
public sealed record RecorderStatusApiResponse(
    bool IsRecording,
    int WindowSeconds,
    string? PluginId,
    int CapturedCommands);
