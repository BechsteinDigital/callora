using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Security;

public sealed class DataProtectionPluginDataProtectorTests
{
    [Fact]
    public void ProtectAndUnprotect_RoundtripsValue()
    {
        var protector = CreateProtector();

        var protectedValue = protector.Protect("voip", "super-geheim");

        Assert.NotEqual("super-geheim", protectedValue);
        Assert.True(protector.TryUnprotect("voip", protectedValue, out var plaintext));
        Assert.Equal("super-geheim", plaintext);
    }

    [Fact]
    public void PayloadsAreIsolatedPerPlugin()
    {
        var protector = CreateProtector();
        var protectedValue = protector.Protect("voip", "super-geheim");

        Assert.False(protector.TryUnprotect("dialer", protectedValue, out _));
    }

    [Fact]
    public void TryUnprotect_ReturnsFalseForPlaintext()
    {
        var protector = CreateProtector();

        Assert.False(protector.TryUnprotect("voip", "kein-geschuetzter-wert", out _));
    }

    private static DataProtectionPluginDataProtector CreateProtector() =>
        new(new EphemeralDataProtectionProvider());
}
