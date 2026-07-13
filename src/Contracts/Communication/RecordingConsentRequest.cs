namespace Callora.Contracts.Communication;

/// <summary>
/// Parameters for one recording-consent interaction: the announcement to
/// play and the DTMF tones that grant or deny consent.
/// </summary>
/// <param name="AnnouncementMediaId">Media asset with the consent announcement; null plays nothing (announcement handled elsewhere).</param>
/// <param name="GrantTone">DTMF tone that grants consent.</param>
/// <param name="DenyTone">DTMF tone that denies consent.</param>
/// <param name="ResponseTimeout">Window to wait for a response before <see cref="RecordingConsentResult.Timeout"/>.</param>
public sealed record RecordingConsentRequest(
    Guid? AnnouncementMediaId = null,
    char GrantTone = '1',
    char DenyTone = '2',
    TimeSpan? ResponseTimeout = null)
{
    /// <summary>Default response window when none is configured.</summary>
    public static readonly TimeSpan DefaultResponseTimeout = TimeSpan.FromSeconds(15);
}
