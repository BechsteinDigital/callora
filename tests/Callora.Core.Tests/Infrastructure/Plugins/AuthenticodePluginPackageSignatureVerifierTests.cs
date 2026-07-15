using Callora.Core.Application.Policies;
using Callora.Core.Infrastructure.Plugins;
using Callora.Core.Tests.Support;

namespace Callora.Core.Tests.Infrastructure.Plugins;

public sealed class AuthenticodePluginPackageSignatureVerifierTests
{
    [Fact]
    public async Task VerifyAsync_UnsignedPlugin_Disallowed_ReturnsInvalid()
    {
        var path = CreateUnsignedPluginFile();
        var sut = new AuthenticodePluginPackageSignatureVerifier(
            new StaticPluginSignatureTrustStore(),
            new BackendHostOptions
            {
                AllowUnsignedPlugins = false
            });

        var result = await sut.VerifyAsync(path);

        Assert.False(result.IsValid);
        Assert.Equal("PLUGIN_PACKAGE_UNSIGNED", result.ErrorCode);
    }

    [Fact]
    public async Task VerifyAsync_UnsignedPlugin_Allowed_ReturnsValid()
    {
        var path = CreateUnsignedPluginFile();
        var sut = new AuthenticodePluginPackageSignatureVerifier(
            new StaticPluginSignatureTrustStore(),
            new BackendHostOptions
            {
                AllowUnsignedPlugins = true
            });

        var result = await sut.VerifyAsync(path);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    private static string CreateUnsignedPluginFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "callora-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, "UnsignedPlugin.dll");
        File.WriteAllText(filePath, "not-a-real-dll");
        return filePath;
    }
}
