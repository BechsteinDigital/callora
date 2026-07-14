using Callora.Host.Backend.Application.Plugins;
using Xunit;

namespace Callora.Host.Backend.Tests.Application.Plugins;

/// <summary>
/// Tier resolution (WP-1): an explicit manifest declaration wins; otherwise the
/// source directory decides; an unknown declaration falls back to the directory.
/// </summary>
public sealed class PluginTierResolverTests
{
    [Theory]
    [InlineData("system", PluginTier.System)]
    [InlineData("System", PluginTier.System)]
    [InlineData("  SYSTEM  ", PluginTier.System)]
    [InlineData("application", PluginTier.Application)]
    public void Resolve_DeclaredTier_Wins(string declared, PluginTier expected)
    {
        // Directory default deliberately opposite to prove the declaration wins.
        var directoryDefault = expected == PluginTier.System ? PluginTier.Application : PluginTier.System;

        Assert.Equal(expected, PluginTierResolver.Resolve(declared, directoryDefault));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nonsense")]
    public void Resolve_MissingOrUnknownDeclaration_FallsBackToDirectory(string? declared)
    {
        Assert.Equal(PluginTier.System, PluginTierResolver.Resolve(declared, PluginTier.System));
        Assert.Equal(PluginTier.Application, PluginTierResolver.Resolve(declared, PluginTier.Application));
    }
}
