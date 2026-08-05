using Callora.Core.Extensibility;

namespace Callora.Core.Application.Policies;

/// <summary>
/// The Content-Security-Policy the shells are served with.
/// </summary>
/// <remarks>
/// <para>
/// Plugin admin bundles are loaded as ordinary scripts into the shell's document, which is what lets
/// them extend the UI deeply — and also gives them the shell's DOM, origin and session. The trust
/// model accepts that for reviewed, fully signed packages (ADR-013), and names a strict CSP as one of
/// the conditions for accepting it. This is that condition.
/// </para>
/// <para>
/// What it actually buys: a plugin bundle cannot pull further code from an arbitrary origin, cannot
/// <c>eval</c> a payload it fetched, and cannot exfiltrate to a host outside the policy. It does not
/// constrain what the bundle does with what it already has — nothing in-document can. That is the
/// same honest line ADR-013 draws for the server side.
/// </para>
/// </remarks>
[CalloraInternal("Host security header policy — not a plugin contract (REV2 §7.2)")]
public static class BackendContentSecurityPolicy
{
    /// <summary>
    /// The default policy. Every source is the shell's own origin except where a browser API forces
    /// otherwise, and each of those exceptions is there for a reason the shells actually have.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><c>script-src 'self'</c> — plugin bundles are same-origin under /plugin-assets. No
    ///   <c>unsafe-eval</c>: nothing in the shells needs it, and it is the difference between a
    ///   bundle running its reviewed code and running code it fetched.</item>
    ///   <item><c>style-src</c> allows inline: Vue writes scoped styles and inline transitions at
    ///   runtime. Stated rather than quietly included — it is the one concession here.</item>
    ///   <item><c>img-src</c> allows <c>data:</c> and <c>blob:</c> for avatars and canvas output;
    ///   <c>media-src blob:</c> for WebRTC streams, which is how a browser hands them to a video
    ///   element.</item>
    ///   <item><c>connect-src</c> includes <c>ws:</c>/<c>wss:</c> because the signalling and media
    ///   sockets are WebSockets on this same origin.</item>
    ///   <item><c>frame-ancestors 'none'</c> and <c>object-src 'none'</c> close clickjacking and
    ///   plugin-object embedding; <c>base-uri 'self'</c> stops a bundle repointing relative URLs.</item>
    /// </list>
    /// </remarks>
    public const string Default =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self' data:; " +
        "media-src 'self' blob:; " +
        "connect-src 'self' ws: wss:; " +
        "worker-src 'self' blob:; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";
}
