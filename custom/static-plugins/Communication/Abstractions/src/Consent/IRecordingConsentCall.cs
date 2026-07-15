namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Optional capability of an <see cref="ICall"/>: legally required consent
/// handling before any recording (§ 201 StGB / Art. 6 GDPR, PLAT-241).
/// Recording features MUST NOT start unless <see cref="ConsentState"/> is
/// <see cref="RecordingConsentState.Granted"/> — the platform treats a
/// missing or pending consent as denial. Channels without this capability
/// cannot host recording at all.
/// </summary>
public interface IRecordingConsentCall
{
    /// <summary>Current consent state of the call.</summary>
    RecordingConsentState ConsentState { get; }

    /// <summary>
    /// Raised on every consent transition; the host relays these as
    /// call.consent-granted / call.consent-denied flow events.
    /// </summary>
    event EventHandler<RecordingConsentChangedEventArgs>? ConsentChanged;

    /// <summary>
    /// Plays the consent announcement and waits for the remote party's DTMF
    /// response. Requires <see cref="CallState.Connected"/>; the call stays
    /// connected during the interaction. Timeout and call end count as
    /// denial.
    /// </summary>
    Task<RecordingConsentResult> RequestRecordingConsentAsync(
        RecordingConsentRequest request,
        CancellationToken cancellationToken = default);
}
