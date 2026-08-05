namespace Callora.Core.Application.Surfaces;

/// <summary>
/// What the surface cookie currently holds (ADR-017 §8.1). The two kinds are stored
/// differently on purpose: a guest is self-contained, an authenticated session is a
/// reference to a revocable server-side record.
/// </summary>
public enum SurfaceSessionEnvelopeKind
{
    /// <summary>A self-contained guest context; the id is the guest subject.</summary>
    Guest = 0,

    /// <summary>A reference to a server-side session; the id is the session id.</summary>
    Authenticated = 1,
}
