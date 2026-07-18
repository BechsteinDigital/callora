using System.Security.Cryptography;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Signing;
using Callora.Core.Application.Policies;
using Callora.Core.Infrastructure.Plugins;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Plugins;

public sealed class ManifestSignaturePluginPackageVerifierTests
{
    private static string CreatePluginDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "callora-verify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "registry.json"), """{ "pluginId": "acme", "version": "1.0.0", "assemblyFileName": "Acme.dll" }""");
        File.WriteAllBytes(Path.Combine(dir, "Acme.dll"), [1, 2, 3, 4]);
        return dir;
    }

    private static void WriteSignature(string dir, ECDsa key)
    {
        var files = new[]
        {
            new PluginSignatureFileHash("Acme.dll", PluginContentHasher.HashFile(Path.Combine(dir, "Acme.dll"))),
            new PluginSignatureFileHash("registry.json", PluginContentHasher.HashFile(Path.Combine(dir, "registry.json"))),
        };
        var unsigned = new PluginSignatureManifest(
            "1.0", "acme", "1.0.0", PluginSignatureAlgorithms.EcdsaP256Sha256,
            PluginSignatureCryptography.ComputeFingerprint(key), files, Signature: null);
        var signature = PluginSignatureCryptography.Sign(PluginSignatureManifestSerializer.SerializeCanonical(unsigned), key);
        File.WriteAllText(
            Path.Combine(dir, "plugin.signature.json"),
            PluginSignatureManifestSerializer.SerializeToFileJson(unsigned with { Signature = signature }));
    }

    private static ManifestSignaturePluginPackageVerifier Verifier(ECDsa? trustedKey, bool allowUnsigned = false)
    {
        var signers = trustedKey is null
            ? Array.Empty<BackendTrustedSignerOptions>()
            : [new BackendTrustedSignerOptions { PublisherId = "callora", DisplayName = "Callora", PublicKey = trustedKey.ExportSubjectPublicKeyInfoPem() }];
        var options = new BackendHostOptions { AllowUnsignedPlugins = allowUnsigned, TrustedSigners = signers };
        return new ManifestSignaturePluginPackageVerifier(new ConfiguredPluginSignatureTrustStore(options), options);
    }

    private static string Assembly(string dir) => Path.Combine(dir, "Acme.dll");

    [Fact]
    public async Task Verify_Allows_ASignedPluginFromATrustedSigner()
    {
        var dir = CreatePluginDir();
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            WriteSignature(dir, key);

            var result = await Verifier(key).VerifyAsync(Assembly(dir));

            Assert.True(result.IsValid);
            Assert.Equal(PluginSignatureCryptography.ComputeFingerprint(key), result.SignerThumbprint);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Verify_Rejects_WhenTheAssemblyIsTamperedAfterSigning()
    {
        var dir = CreatePluginDir();
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            WriteSignature(dir, key);
            await File.WriteAllBytesAsync(Assembly(dir), [9, 9, 9, 9]); // tamper after signing

            var result = await Verifier(key).VerifyAsync(Assembly(dir));

            Assert.False(result.IsValid);
            Assert.Equal(PluginPackageSignatureErrorCodes.ContentHashMismatch, result.ErrorCode);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Verify_Rejects_WhenRegistryIsTamperedAfterSigning()
    {
        var dir = CreatePluginDir();
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            WriteSignature(dir, key);
            // Swap capabilities in registry.json — signing it makes this tamper-evident.
            await File.WriteAllTextAsync(
                Path.Combine(dir, "registry.json"),
                """{ "pluginId": "acme", "version": "1.0.0", "assemblyFileName": "Acme.dll", "capabilities": ["evil"] }""");

            var result = await Verifier(key).VerifyAsync(Assembly(dir));

            Assert.False(result.IsValid);
            Assert.Equal(PluginPackageSignatureErrorCodes.ContentHashMismatch, result.ErrorCode);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Verify_Rejects_AnUntrustedSigner()
    {
        var dir = CreatePluginDir();
        try
        {
            using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var otherTrustedKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            WriteSignature(dir, signingKey);

            var result = await Verifier(otherTrustedKey).VerifyAsync(Assembly(dir));

            Assert.False(result.IsValid);
            Assert.Equal(PluginPackageSignatureErrorCodes.UntrustedSigner, result.ErrorCode);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Verify_Rejects_ACorruptSignature()
    {
        var dir = CreatePluginDir();
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            WriteSignature(dir, key);
            var signaturePath = Path.Combine(dir, "plugin.signature.json");
            var manifest = PluginSignatureManifestSerializer.Deserialize(await File.ReadAllTextAsync(signaturePath))!;
            // Keep a well-formed base64 signature that simply does not verify.
            var forged = manifest with { Signature = Convert.ToBase64String(new byte[64]) };
            await File.WriteAllTextAsync(signaturePath, PluginSignatureManifestSerializer.SerializeToFileJson(forged));

            var result = await Verifier(key).VerifyAsync(Assembly(dir));

            Assert.False(result.IsValid);
            Assert.Equal(PluginPackageSignatureErrorCodes.InvalidSignature, result.ErrorCode);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Verify_Rejects_AManifestWithNoCoveredFiles()
    {
        var dir = CreatePluginDir();
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var unsigned = new PluginSignatureManifest(
                "1.0", "acme", "1.0.0", PluginSignatureAlgorithms.EcdsaP256Sha256,
                PluginSignatureCryptography.ComputeFingerprint(key), Array.Empty<PluginSignatureFileHash>(), Signature: null);
            var signature = PluginSignatureCryptography.Sign(PluginSignatureManifestSerializer.SerializeCanonical(unsigned), key);
            await File.WriteAllTextAsync(
                Path.Combine(dir, "plugin.signature.json"),
                PluginSignatureManifestSerializer.SerializeToFileJson(unsigned with { Signature = signature }));

            // Even a validly signed manifest that covers nothing is rejected (fail-closed).
            var result = await Verifier(key).VerifyAsync(Assembly(dir));

            Assert.False(result.IsValid);
            Assert.Equal(PluginPackageSignatureErrorCodes.InvalidSignature, result.ErrorCode);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Verify_Rejects_AMalformedSignatureFile()
    {
        var dir = CreatePluginDir();
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            await File.WriteAllTextAsync(Path.Combine(dir, "plugin.signature.json"), "{ not valid json");

            var result = await Verifier(key).VerifyAsync(Assembly(dir));

            Assert.False(result.IsValid);
            Assert.Equal(PluginPackageSignatureErrorCodes.InvalidSignature, result.ErrorCode);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Verify_Rejects_AnUnsignedPlugin_ByDefault()
    {
        var dir = CreatePluginDir();
        try
        {
            var result = await Verifier(trustedKey: null).VerifyAsync(Assembly(dir));

            Assert.False(result.IsValid);
            Assert.Equal(PluginPackageSignatureErrorCodes.UnsignedPackage, result.ErrorCode);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Verify_Allows_AnUnsignedPlugin_WhenAllowUnsignedIsSet()
    {
        var dir = CreatePluginDir();
        try
        {
            var result = await Verifier(trustedKey: null, allowUnsigned: true).VerifyAsync(Assembly(dir));

            Assert.True(result.IsValid);
            Assert.Null(result.SignerThumbprint);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
