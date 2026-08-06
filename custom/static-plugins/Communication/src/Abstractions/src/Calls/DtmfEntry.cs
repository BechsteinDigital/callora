namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// The result of one DTMF collection.
/// </summary>
/// <param name="Outcome">How the collection ended.</param>
/// <param name="Digits">
/// What the caller entered, or <see langword="null"/> unless <paramref name="Outcome"/> is
/// <see cref="DtmfEntryOutcome.Completed"/>.
/// <para>
/// <b>Treat this as a credential.</b> A PIN typed into a phone is a bearer secret with a tiny
/// alphabet. It must not reach a log line, an exception message or a diagnostic dump — including in
/// whatever consumes it.
/// </para>
/// </param>
public sealed record DtmfEntry(DtmfEntryOutcome Outcome, string? Digits);
