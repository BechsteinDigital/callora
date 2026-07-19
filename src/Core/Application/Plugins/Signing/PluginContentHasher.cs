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
    /// <summary>
    /// The detached signature file's name. It is the single defined exception to the
    /// package content set — a plugin cannot sign or cover its own signature.
    /// </summary>
    public const string SignatureFileName = "plugin.signature.json";

    public static string HashFile(string absolutePath)
    {
        using var stream = File.OpenRead(absolutePath);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    /// <summary>
    /// Enumerates every file under <paramref name="pluginRoot"/> recursively as
    /// root-relative, forward-slash paths, sorted ordinally, excluding the signature
    /// file itself. This is the single definition of "package content": the signer
    /// hashes exactly this set, and the verifier rejects any on-disk file outside it,
    /// so no executable or other content can live outside the signed manifest.
    /// </summary>
    public static IReadOnlyList<string> EnumeratePackageFiles(string pluginRoot)
    {
        var root = Path.GetFullPath(pluginRoot);
        var results = new List<string>();
        foreach (var absolutePath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(root, absolutePath).Replace('\\', '/');
            if (string.Equals(relativePath, SignatureFileName, StringComparison.Ordinal))
            {
                continue;
            }

            results.Add(relativePath);
        }

        results.Sort(StringComparer.Ordinal);
        return results;
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
