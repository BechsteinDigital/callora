namespace Callora.Core.Application.Options;

/// <summary>
/// Rejects hosting configuration that would start a host which cannot work, at the moment it is
/// configured rather than at the moment someone notices.
/// </summary>
/// <remarks>
/// The values it guards are the kind that fail quietly. A zero resume lifetime issues tickets that
/// are already expired; a zero payload limit refuses every ticket. Neither raises an error anywhere —
/// they simply mean nobody ever reconnects, which reads like a bug in the plugin rather than a typo
/// in a config file.
/// </remarks>
public static class CalloraHostingOptionsValidator
{
    /// <summary>
    /// Largest payload limit the host will accept. A resume payload carries identity, not state, and
    /// a limit past this turns the ticket table into a document store no purge was sized for.
    /// </summary>
    public const int MaxSessionResumePayloadBytes = 64 * 1024;

    /// <summary>Throws <see cref="ArgumentException"/> when the options cannot produce a working host.</summary>
    /// <param name="options">The configured options.</param>
    public static void Validate(CalloraHostingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.PluginDrainTimeout < TimeSpan.Zero)
        {
            throw new ArgumentException(
                $"{nameof(CalloraHostingOptions.PluginDrainTimeout)} cannot be negative. Use " +
                "TimeSpan.Zero to skip draining entirely.",
                nameof(options));
        }

        if (options.RuntimeCapabilityGracePeriod < TimeSpan.Zero)
        {
            throw new ArgumentException(
                $"{nameof(CalloraHostingOptions.RuntimeCapabilityGracePeriod)} cannot be negative. Use " +
                "TimeSpan.Zero to apply a capability loss immediately.",
                nameof(options));
        }

        if (options.SessionResumeMaxLifetime <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                $"{nameof(CalloraHostingOptions.SessionResumeMaxLifetime)} must be positive. A " +
                "non-positive value clamps every resume ticket to expired-on-issue, so no client ever " +
                "reconnects and nothing reports why.",
                nameof(options));
        }

        if (options.SessionResumeMaxPayloadBytes is <= 0 or > MaxSessionResumePayloadBytes)
        {
            throw new ArgumentException(
                $"{nameof(CalloraHostingOptions.SessionResumeMaxPayloadBytes)} must be between 1 and " +
                $"{MaxSessionResumePayloadBytes}. Zero or less refuses every ticket; more than the " +
                "maximum stops the payload being an identity and starts it being storage.",
                nameof(options));
        }
    }
}
