using Callora.Core.Extensibility;

namespace Callora.Core.Application.Surfaces.Contracts;

/// <summary>
/// Publishes a context value to the surfaces a visitor has open, so a server-side event reaches
/// the views that declared they need it.
/// <para>
/// This is what makes <c>ProvidesContexts</c> more than documentation. A block binds a control to
/// <c>communication.active-call/v1</c> and updates when a call arrives; nobody writes a socket, a
/// reconnect or a message format — the runtime keeps one connection and feeds the local context
/// channel from it.
/// </para>
/// <para>
/// Deliberately one-way. A browser cannot publish here: everything in the tab is visible to
/// DevTools and to every script on the page, so a value that arrives from there carries no
/// authority. The host is the only publisher, and a client that wants to change something uses
/// the API for it (design §5.5).
/// </para>
/// </summary>
[CalloraExtensible("Extension point — resolve to publish surface context from server-side events")]
public interface ISurfaceContextBroadcaster
{
    /// <summary>
    /// Publishes <paramref name="value"/> under <paramref name="key"/> to every connection the
    /// address covers. Serialised once for all recipients; a connection that has gone away is
    /// dropped rather than retried.
    /// </summary>
    /// <param name="address">Who receives it — see <see cref="SurfaceContextAddress"/>.</param>
    /// <param name="key">A namespaced, versioned key such as <c>communication.active-call/v1</c>.</param>
    /// <param name="value">The value, serialised to JSON. Null clears the key.</param>
    void Publish(SurfaceContextAddress address, string key, object? value);
}
