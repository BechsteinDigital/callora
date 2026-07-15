namespace Callora.Core.Application.Notifications;

public sealed record NotificationSnapshot(
    Guid Id,
    string? WorkspaceKey,
    string Title,
    string Message,
    string Level,
    bool IsRead,
    DateTimeOffset CreatedAtUtc);
