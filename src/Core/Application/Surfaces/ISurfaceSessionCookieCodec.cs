namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Turns a <see cref="SurfaceSessionEnvelope"/> into the opaque cookie value and back
/// (ADR-017 §8.2). The implementation signs and encrypts, so a visitor can neither
/// read nor forge their own context — a guest id anyone could mint at will would let
/// them adopt someone else's cart.
/// </summary>
public interface ISurfaceSessionCookieCodec
{
    /// <summary>Protects an envelope into a cookie value.</summary>
    /// <param name="envelope">Envelope to protect.</param>
    string Protect(SurfaceSessionEnvelope envelope);

    /// <summary>
    /// Unprotects a cookie value, or returns null when it is absent, tampered with,
    /// or protected under a key that no longer exists.
    /// </summary>
    /// <param name="value">Cookie value as sent by the client.</param>
    SurfaceSessionEnvelope? Unprotect(string? value);
}
