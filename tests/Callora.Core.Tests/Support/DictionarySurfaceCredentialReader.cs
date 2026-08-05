using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Surfaces;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Credential reader over an in-memory map, standing in for the request's headers
/// and cookies.
/// </summary>
public sealed class DictionarySurfaceCredentialReader : ISurfaceCredentialReader
{
    private readonly Dictionary<(SurfaceIdentityCredentialKind Kind, string Name), string> _values = new();

    /// <summary>Adds one readable credential.</summary>
    /// <param name="kind">Header or cookie.</param>
    /// <param name="name">Name to read it under.</param>
    /// <param name="value">Value the request carries.</param>
    public DictionarySurfaceCredentialReader With(SurfaceIdentityCredentialKind kind, string name, string value)
    {
        _values[(kind, name)] = value;
        return this;
    }

    public string? Read(SurfaceIdentityCredentialKind kind, string name) =>
        _values.GetValueOrDefault((kind, name));
}
