namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// What a redeemed resume ticket hands back: the plugin's own description of the session, so it can
/// rebuild it.
/// </summary>
/// <param name="SessionKind">The kind the plugin named when it issued the ticket.</param>
/// <param name="Payload">The payload verbatim. The host never read it and never changed it.</param>
/// <param name="WorkspaceKey">Workspace recorded at issue time, or null when the session had none.</param>
/// <param name="IssuedAtUtc">
/// When the promise was made. Lets a plugin see how long the client was away, which is the difference
/// between a tunnel and a restart.
/// </param>
public sealed record HostSessionResume(
    string SessionKind,
    string Payload,
    string? WorkspaceKey,
    DateTimeOffset IssuedAtUtc);
