namespace Callora.Host.Backend.Application.Retention;

/// <summary>
/// Configurable data-retention windows (GDPR storage limitation, Art. 5(1)(e)).
/// Completed background jobs carry PII in their payloads (phone numbers,
/// e-mail addresses) and must not accumulate indefinitely.
/// </summary>
public sealed class RetentionOptions
{
    /// <summary>Master switch for the recurring cleanup job.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Interval between cleanup sweeps.</summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// How long succeeded/failed background jobs (including their payloads)
    /// are kept after completion.
    /// </summary>
    public TimeSpan CompletedJobRetention { get; set; } = TimeSpan.FromDays(14);

    /// <summary>How long in-app notifications are kept.</summary>
    public TimeSpan NotificationRetention { get; set; } = TimeSpan.FromDays(90);
}
