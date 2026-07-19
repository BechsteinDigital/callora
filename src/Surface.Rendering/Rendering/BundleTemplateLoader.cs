using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Callora.Surface.Rendering.Rendering;

/// <summary>
/// Scriban template loader confined to a surface's resolved bundle chain
/// (ADR-015 §8, E2). Only bundles in scope for this render resolve, and every
/// resolved file path is canonicalised and verified to stay UNDER its bundle
/// root — a template can never escape its bundle via <c>../</c> or an absolute
/// path. Recursion depth is bounded by the context's RecursiveLimit.
/// </summary>
internal sealed class BundleTemplateLoader : ITemplateLoader
{
    private readonly ISurfaceTemplateBundleProvider _provider;
    private readonly HashSet<string> _bundlesInScope;

    public BundleTemplateLoader(ISurfaceTemplateBundleProvider provider, IEnumerable<string> bundlesInScope)
    {
        _provider = provider;
        _bundlesInScope = new HashSet<string>(bundlesInScope, StringComparer.OrdinalIgnoreCase);
    }

    public string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName) || templateName[0] != '@')
        {
            throw new ScriptRuntimeException(callerSpan, $"Template include must be '@bundle/path', got '{templateName}'.");
        }

        var slash = templateName.IndexOf('/', StringComparison.Ordinal);
        if (slash <= 1 || slash == templateName.Length - 1)
        {
            throw new ScriptRuntimeException(callerSpan, $"Template include must be '@bundle/path', got '{templateName}'.");
        }

        var bundleId = templateName[1..slash];
        var relativePath = templateName[(slash + 1)..];

        if (!_bundlesInScope.Contains(bundleId))
        {
            throw new ScriptRuntimeException(callerSpan, $"Template bundle '{bundleId}' is not in scope for this surface.");
        }

        if (!_provider.TryGetBundleRoot(bundleId, out var root) || string.IsNullOrWhiteSpace(root))
        {
            throw new ScriptRuntimeException(callerSpan, $"Unknown template bundle '{bundleId}'.");
        }

        var rootFullPath = Path.GetFullPath(root);
        // Path.Combine lets an absolute relativePath win; GetFullPath then normalises
        // any '..' — the containment check below rejects everything outside the root.
        var candidate = Path.GetFullPath(Path.Combine(rootFullPath, relativePath));
        if (!IsUnderRoot(rootFullPath, candidate))
        {
            throw new ScriptRuntimeException(callerSpan, $"Template path escapes bundle '{bundleId}'.");
        }

        return candidate;
    }

    public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath)
    {
        if (!File.Exists(templatePath))
        {
            throw new ScriptRuntimeException(callerSpan, "Included template was not found.");
        }

        return File.ReadAllText(templatePath);
    }

    public async ValueTask<string?> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath)
    {
        if (!File.Exists(templatePath))
        {
            throw new ScriptRuntimeException(callerSpan, "Included template was not found.");
        }

        return await File.ReadAllTextAsync(templatePath).ConfigureAwait(false);
    }

    // DECISION: containment is textual (GetFullPath normalises '..' but does not
    // resolve symlinks). A symlink INSIDE a bundle root that points outside would
    // pass this check — an accepted residual boundary under the curated/self-hosted
    // trust model (ADR-013: bundle content is not an untrusted third-party upload).
    // Harden with real-path resolution before accepting bundles from untrusted sources.
    private static bool IsUnderRoot(string rootFullPath, string candidateFullPath)
    {
        var normalizedRoot = rootFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(candidateFullPath, normalizedRoot, StringComparison.Ordinal) ||
               candidateFullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
