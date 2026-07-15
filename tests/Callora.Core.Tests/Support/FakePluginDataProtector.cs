using Callora.Host.PluginContracts.Application.Secrets;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Reversible fake protector: prefixes values per plugin so tests can assert
/// at-rest encryption semantics without real cryptography.
/// </summary>
public sealed class FakePluginDataProtector : IPluginDataProtector
{
    public string Protect(string pluginId, string plaintext) =>
        $"protected:{pluginId}:{plaintext}";

    public bool TryUnprotect(string pluginId, string protectedValue, out string plaintext)
    {
        var prefix = $"protected:{pluginId}:";
        if (protectedValue.StartsWith(prefix, StringComparison.Ordinal))
        {
            plaintext = protectedValue[prefix.Length..];
            return true;
        }

        plaintext = string.Empty;
        return false;
    }
}
