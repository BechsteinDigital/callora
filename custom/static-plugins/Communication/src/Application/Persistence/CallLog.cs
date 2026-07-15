namespace Callora.Plugin.Communication.Application.Persistence;

/// <summary>
/// One completed call, stored as a real typed entity in the voice plugin's
/// own database schema (PLAT-260) — the plugin owns its data via EF Core
/// instead of jsonb documents.
/// </summary>
public sealed class CallLog
{
    public Guid Id { get; set; }

    public string WorkspaceKey { get; set; } = string.Empty;

    public string CallId { get; set; } = string.Empty;

    public string ChannelId { get; set; } = string.Empty;

    public string Direction { get; set; } = string.Empty;

    public string TargetValue { get; set; } = string.Empty;

    public string? TargetDisplayName { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset EndedAtUtc { get; set; }
}
