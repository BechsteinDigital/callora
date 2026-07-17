namespace Callora.Core.Application.Notifications.Contracts;

/// <summary>
/// Severity levels of in-app notifications.
/// </summary>
public static class NotificationLevels
{
    /// <summary>Neutral informational message.</summary>
    public const string Info = "info";

    /// <summary>Confirms an operation completed successfully.</summary>
    public const string Success = "success";

    /// <summary>Signals a condition that needs attention but is not an error.</summary>
    public const string Warning = "warning";

    /// <summary>Reports a failure the operator should act on.</summary>
    public const string Error = "error";
}
