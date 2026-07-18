using System.Security.Cryptography;
using Callora.Core.Application.Plugins.Signing;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins.Signing;

public sealed class PluginSignatureManifestTests
{
    private static PluginSignatureManifest Manifest(IReadOnlyList<PluginSignatureFileHash> files, string fingerprint, string? signature = null) =>
        new(
            SchemaVersion: "1.0",
            PluginId: "acme",
            Version: "1.2.3",
            Algorithm: PluginSignatureAlgorithms.EcdsaP256Sha256,
            SignerFingerprint: fingerprint,
            Files: files,
            Signature: signature);

    [Fact]
    public void SerializeCanonical_IsIndependentOfFileOrder()
    {
        var a = new PluginSignatureFileHash("a.dll", "AA");
        var b = new PluginSignatureFileHash("registry.json", "BB");

        var forward = PluginSignatureManifestSerializer.SerializeCanonical(Manifest([a, b], "FP"));
        var reversed = PluginSignatureManifestSerializer.SerializeCanonical(Manifest([b, a], "FP"));

        Assert.Equal(forward, reversed);
    }

    [Fact]
    public void FileRoundTrip_CanonicalizesIndependentlyOfStoredFileOrder()
    {
        // The B2 verifier reads plugin.signature.json from disk and re-canonicalizes.
        // A file that happens to store the covered files in a different order must
        // still yield identical canonical bytes, or verification would break.
        var a = new PluginSignatureFileHash("a.dll", "AA");
        var b = new PluginSignatureFileHash("registry.json", "BB");

        var json1 = PluginSignatureManifestSerializer.SerializeToFileJson(Manifest([a, b], "FP", signature: "sig"));
        var json2 = PluginSignatureManifestSerializer.SerializeToFileJson(Manifest([b, a], "FP", signature: "sig"));

        var canonical1 = PluginSignatureManifestSerializer.SerializeCanonical(PluginSignatureManifestSerializer.Deserialize(json1)!);
        var canonical2 = PluginSignatureManifestSerializer.SerializeCanonical(PluginSignatureManifestSerializer.Deserialize(json2)!);

        Assert.Equal(canonical1, canonical2);
    }

    [Fact]
    public void SerializeCanonical_ExcludesTheSignatureField()
    {
        var files = new[] { new PluginSignatureFileHash("a.dll", "AA") };
        var unsigned = PluginSignatureManifestSerializer.SerializeCanonical(Manifest(files, "FP", signature: null));
        var signed = PluginSignatureManifestSerializer.SerializeCanonical(Manifest(files, "FP", signature: "a-signature"));

        // The signature is not part of what gets signed, so it must not change the canonical bytes.
        Assert.Equal(unsigned, signed);
    }

    [Fact]
    public void SignThenVerify_RoundTrips_ThroughSerialization()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var fingerprint = PluginSignatureCryptography.ComputeFingerprint(key);
        var files = new[]
        {
            new PluginSignatureFileHash("acme.dll", "1111"),
            new PluginSignatureFileHash("registry.json", "2222"),
        };

        var canonical = PluginSignatureManifestSerializer.SerializeCanonical(Manifest(files, fingerprint));
        var signature = PluginSignatureCryptography.Sign(canonical, key);
        var json = PluginSignatureManifestSerializer.SerializeToFileJson(Manifest(files, fingerprint, signature));

        var roundTripped = PluginSignatureManifestSerializer.Deserialize(json);
        Assert.NotNull(roundTripped);
        Assert.Equal(signature, roundTripped!.Signature);

        var recomputedCanonical = PluginSignatureManifestSerializer.SerializeCanonical(roundTripped);
        Assert.True(PluginSignatureCryptography.Verify(recomputedCanonical, roundTripped.Signature!, key));
    }

    [Fact]
    public void Verify_Fails_WhenACoveredHashIsTampered()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var fingerprint = PluginSignatureCryptography.ComputeFingerprint(key);
        var files = new[] { new PluginSignatureFileHash("acme.dll", "1111") };
        var signature = PluginSignatureCryptography.Sign(
            PluginSignatureManifestSerializer.SerializeCanonical(Manifest(files, fingerprint)),
            key);

        var tamperedFiles = new[] { new PluginSignatureFileHash("acme.dll", "9999") };
        var tamperedCanonical = PluginSignatureManifestSerializer.SerializeCanonical(Manifest(tamperedFiles, fingerprint));

        Assert.False(PluginSignatureCryptography.Verify(tamperedCanonical, signature, key));
    }

    [Fact]
    public void Verify_Fails_WithADifferentKey()
    {
        using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var files = new[] { new PluginSignatureFileHash("acme.dll", "1111") };
        var canonical = PluginSignatureManifestSerializer.SerializeCanonical(
            Manifest(files, PluginSignatureCryptography.ComputeFingerprint(signingKey)));
        var signature = PluginSignatureCryptography.Sign(canonical, signingKey);

        Assert.False(PluginSignatureCryptography.Verify(canonical, signature, otherKey));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForMalformedSignature()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.False(PluginSignatureCryptography.Verify("data"u8, "not-base64-!!", key));
    }

    [Fact]
    public void ComputeFingerprint_IsStablePerKey_AndDiffersAcrossKeys()
    {
        using var key1 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var key2 = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        Assert.Equal(PluginSignatureCryptography.ComputeFingerprint(key1), PluginSignatureCryptography.ComputeFingerprint(key1));
        Assert.NotEqual(PluginSignatureCryptography.ComputeFingerprint(key1), PluginSignatureCryptography.ComputeFingerprint(key2));
    }

    [Fact]
    public void ResolveContained_RejectsEscapingPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "callora-sign-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<ArgumentException>(() => PluginContentHasher.ResolveContained(root, "../secret"));
        // A normal sub-path resolves under the root.
        var resolved = PluginContentHasher.ResolveContained(root, "acme.dll");
        Assert.StartsWith(Path.GetFullPath(root), resolved, StringComparison.Ordinal);
    }
}
