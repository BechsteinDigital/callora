namespace Callora.Core.Application.Plugins;

/// <summary>
/// Encrypts and decrypts opaque per-plugin payloads at rest.
/// </summary>
/// <remarks>
/// <para>
/// A port, not a wrapper for convenience. The application layer needs the guarantee — this payload
/// is unreadable in the database, and one plugin's payload is unreadable by another — without
/// knowing which library provides it. The implementation lives in Infrastructure.
/// </para>
/// <para>
/// The plugin id is a parameter rather than a constructor dependency because a single host protects
/// payloads for many plugins, and the separation between them is exactly what the implementation
/// has to encode.
/// </para>
/// </remarks>
public interface IPluginPayloadProtector
{
    /// <summary>Protects <paramref name="payload"/> so only the same plugin can read it back.</summary>
    string Protect(string pluginId, string payload);

    /// <summary>
    /// Reads a protected payload back. Returns <see langword="false"/> when it cannot be read at all
    /// — rotated keys, a database restored from another deployment, or a value protected for a
    /// different plugin.
    /// </summary>
    /// <remarks>
    /// A result rather than an exception: to the caller, "cannot read this" is an ordinary answer
    /// that belongs on the same path as "no such row", not an error to handle separately.
    /// </remarks>
    bool TryUnprotect(string pluginId, string protectedPayload, out string payload);
}
