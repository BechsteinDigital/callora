namespace Callora.Core.Application.Plugins;

/// <summary>
/// Resolves a plugin's tier: an explicit <c>tier</c> declaration in the manifest
/// wins; otherwise the source directory decides (custom/static-plugins ⇒ System,
/// custom/plugins ⇒ Application). An unrecognized declaration falls back to the
/// directory default.
/// </summary>
public static class PluginTierResolver
{
    public static PluginTier Resolve(string? declaredTier, PluginTier directoryDefault)
    {
        if (!string.IsNullOrWhiteSpace(declaredTier))
        {
            var normalized = declaredTier.Trim();
            if (normalized.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                return PluginTier.System;
            }

            if (normalized.Equals("application", StringComparison.OrdinalIgnoreCase))
            {
                return PluginTier.Application;
            }
        }

        return directoryDefault;
    }
}
