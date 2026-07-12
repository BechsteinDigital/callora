namespace Callora.Host.Backend.Domain.Notifications;

/// <summary>
/// One in-app notification shown in the admin notification center.
/// </summary>
public sealed class NotificationEntry
{
    public Guid Id { get; set; }

    /// <summary>Null targets all operators; otherwise scoped to one workspace.</summary>
    public string? WorkspaceKey { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    /// <summary>info, success, warning or error.</summary>
    public string Level { get; set; } = "info";

    public bool IsRead { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
