using Callora.Core.Application.Secrets.Contracts;
using Callora.Core.Extensibility;
using Microsoft.AspNetCore.DataProtection;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Plugin data encryption backed by the ASP.NET Core Data Protection API.
/// Each plugin gets its own protector purpose, so payloads are not
/// exchangeable between plugins.
/// </summary>
[CalloraInternal("DataProtection implementation — consume the IPluginDataProtector contract instead (REV2 §7.2)")]
public sealed class DataProtectionPluginDataProtector(IDataProtectionProvider dataProtectionProvider)
    : IPluginDataProtector
{
    private const string PurposeRoot = "Callora.PluginData";

    public string Protect(string pluginId, string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(plaintext);

        return CreateProtector(pluginId).Protect(plaintext);
    }

    public bool TryUnprotect(string pluginId, string protectedValue, out string plaintext)
    {
        plaintext = string.Empty;
        if (string.IsNullOrWhiteSpace(pluginId) || string.IsNullOrEmpty(protectedValue))
        {
            return false;
        }

        try
        {
            plaintext = CreateProtector(pluginId).Unprotect(protectedValue);
            return true;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private IDataProtector CreateProtector(string pluginId) =>
        dataProtectionProvider.CreateProtector(PurposeRoot, pluginId.Trim().ToLowerInvariant());
}
