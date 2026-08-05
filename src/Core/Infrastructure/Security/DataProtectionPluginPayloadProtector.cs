using System.Security.Cryptography;
using Callora.Core.Application.Plugins;
using Microsoft.AspNetCore.DataProtection;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Implements <see cref="IPluginPayloadProtector"/> on ASP.NET Core Data Protection.
/// </summary>
/// <remarks>
/// The purpose string carries the plugin id, which is what makes the separation cryptographic rather
/// than a matter of the caller passing the right argument: a payload protected for one plugin cannot
/// be unprotected for another even if the id is wrong or forged.
/// </remarks>
public sealed class DataProtectionPluginPayloadProtector(IDataProtectionProvider dataProtectionProvider)
    : IPluginPayloadProtector
{
    private const string PurposePrefix = "Callora.PluginPayload.v1:";

    public string Protect(string pluginId, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(payload);

        return ProtectorFor(pluginId).Protect(payload);
    }

    public bool TryUnprotect(string pluginId, string protectedPayload, out string payload)
    {
        payload = string.Empty;
        if (string.IsNullOrWhiteSpace(pluginId) || string.IsNullOrEmpty(protectedPayload))
        {
            return false;
        }

        try
        {
            payload = ProtectorFor(pluginId).Unprotect(protectedPayload);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private IDataProtector ProtectorFor(string pluginId) =>
        dataProtectionProvider.CreateProtector(PurposePrefix + pluginId);
}
