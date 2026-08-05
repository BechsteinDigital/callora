using System.Text.Json;
using Callora.Core.Application.Surfaces;
using Microsoft.AspNetCore.DataProtection;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Protects the surface cookie with the host's data-protection key ring
/// (ADR-017 §8.2). Signing and encrypting is what makes a guest context safe to keep
/// client-side: the visitor can neither read their own envelope nor mint one, so a
/// guest subject cannot be chosen — only received.
/// </summary>
public sealed class DataProtectionSurfaceSessionCookieCodec : ISurfaceSessionCookieCodec
{
    // Versioned purpose: rotating it invalidates every previously issued cookie,
    // which is exactly what a breaking envelope change should do.
    private const string ProtectorPurpose = "Callora.Surface.Session.v1";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDataProtector _protector;

    /// <summary>Creates the codec over the host key ring.</summary>
    /// <param name="dataProtectionProvider">Provider of the host's key ring.</param>
    public DataProtectionSurfaceSessionCookieCodec(IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
    }

    /// <inheritdoc />
    public string Protect(SurfaceSessionEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return _protector.Protect(JsonSerializer.Serialize(envelope, SerializerOptions));
    }

    /// <inheritdoc />
    public SurfaceSessionEnvelope? Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SurfaceSessionEnvelope>(
                _protector.Unprotect(value),
                SerializerOptions);
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException
                                       or JsonException
                                       or FormatException)
        {
            // Tampered, truncated, or protected under a key that is gone. All three
            // mean the same thing to the caller: there is no usable context, so the
            // request continues as a fresh visitor instead of failing.
            return null;
        }
    }
}
