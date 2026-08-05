using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Reads one declared credential off the current request. The port exists so identity
/// resolution stays free of the transport: the HTTP adapter knows about headers and
/// cookies, the resolver only knows that a provider declared a source and either got
/// a value or did not (ADR-017 §4).
/// </summary>
public interface ISurfaceCredentialReader
{
    /// <summary>
    /// Returns the value of the named header or cookie, or null when the request does
    /// not carry it.
    /// </summary>
    /// <param name="kind">Whether to read a header or a cookie.</param>
    /// <param name="name">Name of the header or cookie.</param>
    string? Read(SurfaceIdentityCredentialKind kind, string name);
}
