using Callora.Core.Application.Plugins;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

public sealed class PluginSignatureStateMapperTests
{
    [Fact]
    public void Map_SignedTrusted_WhenValidWithAFingerprint()
    {
        var result = new PluginPackageSignatureVerificationResult(IsValid: true, SignerThumbprint: "ABC");
        Assert.Equal(PluginSignatureStates.SignedTrusted, PluginSignatureStateMapper.Map(result));
    }

    [Fact]
    public void Map_Unsigned_WhenValidWithoutAFingerprint()
    {
        // Unsigned-but-allowed (AllowUnsignedPlugins) is still "unsigned" for trust.
        var result = new PluginPackageSignatureVerificationResult(IsValid: true, SignerThumbprint: null);
        Assert.Equal(PluginSignatureStates.NotSigned, PluginSignatureStateMapper.Map(result));
    }

    [Theory]
    [InlineData(PluginPackageSignatureErrorCodes.UnsignedPackage, PluginSignatureStates.NotSigned)]
    [InlineData(PluginPackageSignatureErrorCodes.UntrustedSigner, PluginSignatureStates.Untrusted)]
    [InlineData(PluginPackageSignatureErrorCodes.Revoked, PluginSignatureStates.Revoked)]
    [InlineData(PluginPackageSignatureErrorCodes.ContentHashMismatch, PluginSignatureStates.ContentHashMismatch)]
    [InlineData(PluginPackageSignatureErrorCodes.InvalidSignature, PluginSignatureStates.Invalid)]
    [InlineData("SOMETHING_UNKNOWN", PluginSignatureStates.Invalid)]
    public void Map_ErrorCode_ToState(string errorCode, string expectedState)
    {
        var result = new PluginPackageSignatureVerificationResult(IsValid: false, ErrorCode: errorCode);
        Assert.Equal(expectedState, PluginSignatureStateMapper.Map(result));
    }
}
