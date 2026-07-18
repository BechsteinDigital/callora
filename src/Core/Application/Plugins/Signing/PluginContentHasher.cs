using System.Security.Cryptography;
using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins.Signing;

/// <summary>
/// Hashes plugin files for the signature manifest. Paths are resolved under the
/// plugin root and rejected if they escape it, so a crafted manifest path
/// (e.g. "../secret") can never read outside the plugin.
/// </summary>
[CalloraInternal("Plugin content hashing — not a plugin contract (REV2 §7.2)")]
public static class PluginContentHasher
{
    public static string HashFile(string absolutePath)
    {
        using var stream = File.OpenRead(absolutePath);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    /// <summary>
    /// Resolves <paramref name="relativePath"/> under <paramref name="pluginRoot"/>,
    /// throwing if it escapes the root.
    /// </summary>
    public static string ResolveContained(string pluginRoot, string relativePath)
    {
        var root = Path.GetFullPath(pluginRoot);
        var target = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!string.Equals(target, root, StringComparison.Ordinal) &&
            !target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Path '{relativePath}' escapes the plugin root.", nameof(relativePath));
        }

        return target;
    }
}
