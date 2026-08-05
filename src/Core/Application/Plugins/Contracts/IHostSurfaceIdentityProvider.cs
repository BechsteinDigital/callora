using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Authenticates the visitors of a surface on behalf of the plugin that owns them —
/// a CRM's leads, a portal's customers, a clinic's patients (ADR-017 §4). The plugin
/// answers <em>who is this</em>; the host normalises the answer, binds it to tenant,
/// workspace, surface and audience, and carries it through rendering, the surface API
/// and WebSocket authorisation.
/// <para>
/// A provider is bound to a surface by operator assignment, not by self-declaration:
/// surface keys are operator data a shipped plugin cannot know (ADR-017 §5). To be
/// assignable, the plugin declares the <c>surface.identity</c> capability in its
/// <c>registry.json</c>.
/// </para>
/// <para>
/// The call runs under a hard deadline. A timeout, a thrown exception or an invalid
/// result is a provider failure — never a silent fall-through to anonymous, which on
/// a protected surface would be an access leak (ADR-017 §6).
/// </para>
/// </summary>
[CalloraExtensible("Extension point — implement to authenticate a surface's own visitors (ADR-017 §4)")]
public interface IHostSurfaceIdentityProvider
{
    /// <summary>
    /// Stable plugin identifier owning this provider. Must match the plugin an
    /// operator assigned to the surface, otherwise the provider is not consulted.
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// The request values this provider needs. The host forwards these and nothing
    /// else; declaring a source is how a provider asks for it.
    /// </summary>
    IReadOnlyList<SurfaceIdentityCredentialSource> CredentialSources { get; }

    /// <summary>
    /// Decides whether the request carries a recognisable visitor.
    /// </summary>
    /// <param name="request">Surface context plus the declared credential values.</param>
    /// <param name="cancellationToken">Cancelled when the host's execution deadline elapses.</param>
    ValueTask<HostSurfaceIdentityResult> AuthenticateAsync(
        HostSurfaceIdentityRequest request,
        CancellationToken cancellationToken = default);
}
