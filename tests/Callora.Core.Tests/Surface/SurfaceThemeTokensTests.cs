using Callora.Surface.Rendering;
using Xunit;

namespace Callora.Core.Tests.Surface;

public sealed class SurfaceThemeTokensTests
{
    [Fact]
    public void Compose_MergesEffectiveValuesAndMetaTokens()
    {
        var effective = new Dictionary<string, string>
        {
            ["primaryColor"] = "#e4002b",
            ["spaceUnit"] = "1.25rem",
        };

        var tokens = SurfaceThemeTokens.Compose("acme.brand-theme", "1.0.0", effective);

        Assert.Equal("#e4002b", tokens["primaryColor"]);
        Assert.Equal("1.25rem", tokens["spaceUnit"]);
        Assert.Equal("acme.brand-theme", tokens[SurfaceThemeTokens.ThemePluginIdKey]);
        Assert.Equal("1.0.0", tokens[SurfaceThemeTokens.ThemeVersionKey]);
    }

    [Fact]
    public void Compose_ReservedMetaTokensWinOverSameNamedSettingKey()
    {
        // A theme setting must never be able to spoof the assigned-plugin identity.
        var effective = new Dictionary<string, string>
        {
            [SurfaceThemeTokens.ThemePluginIdKey] = "attacker.theme",
            [SurfaceThemeTokens.ThemeVersionKey] = "9.9.9",
        };

        var tokens = SurfaceThemeTokens.Compose("real.theme", "1.0.0", effective);

        Assert.Equal("real.theme", tokens[SurfaceThemeTokens.ThemePluginIdKey]);
        Assert.Equal("1.0.0", tokens[SurfaceThemeTokens.ThemeVersionKey]);
    }

    [Fact]
    public void Compose_WithoutTheme_ProducesEmptyTokens()
    {
        var tokens = SurfaceThemeTokens.Compose(null, null, null);

        Assert.Empty(tokens);
    }

    [Fact]
    public void Compose_WithThemeButNoValues_ProducesOnlyMetaTokens()
    {
        var tokens = SurfaceThemeTokens.Compose("acme.brand-theme", "2.0.0", effectiveValues: null);

        Assert.Equal(2, tokens.Count);
        Assert.Equal("acme.brand-theme", tokens[SurfaceThemeTokens.ThemePluginIdKey]);
        Assert.Equal("2.0.0", tokens[SurfaceThemeTokens.ThemeVersionKey]);
    }

    [Fact]
    public void Compose_SkipsBlankKeys()
    {
        var effective = new Dictionary<string, string>
        {
            [" "] = "ignored",
            ["ok"] = "kept",
        };

        var tokens = SurfaceThemeTokens.Compose(null, null, effective);

        Assert.False(tokens.ContainsKey(" "));
        Assert.Equal("kept", tokens["ok"]);
        Assert.Single(tokens);
    }
}
