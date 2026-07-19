using System.Security.Cryptography;
using Callora.Core.Application.Plugins.Signing;
using Callora.Host.Cli.Application;
using Xunit;

namespace Callora.Core.Tests.Cli;

public sealed class PluginSignCliTests
{
    [Fact]
    public async Task PluginSign_ProducesAVerifiableSignatureCoveringEveryFileIncludingSubdirectories()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "callora-sign-" + Guid.NewGuid().ToString("N"));
        // The signing key lives outside the plugin directory — it must never become part
        // of the signed package.
        var keyDir = Path.Combine(Path.GetTempPath(), "callora-key-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(keyDir);
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "registry.json"),
                """{ "pluginId": "acme", "version": "1.0.0", "assemblyFileName": "Acme.dll" }""");
            await File.WriteAllBytesAsync(Path.Combine(tempDir, "Acme.dll"), [1, 2, 3, 4]);
            // A dependent file in a subdirectory must be covered too.
            Directory.CreateDirectory(Path.Combine(tempDir, "lib"));
            await File.WriteAllBytesAsync(Path.Combine(tempDir, "lib", "Dep.dll"), [5, 6, 7, 8]);

            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var keyPath = Path.Combine(keyDir, "signing.pem");
            await File.WriteAllTextAsync(keyPath, key.ExportPkcs8PrivateKeyPem());

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var exitCode = await CalloraCliApplication.RunAsync(
                ["plugin", "sign", "--plugin", tempDir, "--key", keyPath],
                stdout,
                stderr,
                tempDir,
                CancellationToken.None);

            Assert.Equal(0, exitCode);

            var signaturePath = Path.Combine(tempDir, "plugin.signature.json");
            Assert.True(File.Exists(signaturePath));

            var manifest = PluginSignatureManifestSerializer.Deserialize(await File.ReadAllTextAsync(signaturePath));
            Assert.NotNull(manifest);
            Assert.Equal("acme", manifest!.PluginId);
            Assert.Equal(PluginSignatureCryptography.ComputeFingerprint(key), manifest.SignerFingerprint);
            Assert.Contains(manifest.Files, x => x.Path == "Acme.dll");
            Assert.Contains(manifest.Files, x => x.Path == "registry.json");
            Assert.Contains(manifest.Files, x => x.Path == "lib/Dep.dll");
            // The out-of-tree signing key is not part of the package.
            Assert.DoesNotContain(manifest.Files, x => x.Path.Contains("signing.pem", StringComparison.Ordinal));

            var canonical = PluginSignatureManifestSerializer.SerializeCanonical(manifest);
            Assert.True(PluginSignatureCryptography.Verify(canonical, manifest.Signature!, key));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }

            if (Directory.Exists(keyDir))
            {
                Directory.Delete(keyDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PluginSign_Fails_WhenRegistryIsMissing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "callora-sign-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var keyPath = Path.Combine(tempDir, "signing.pem");
            await File.WriteAllTextAsync(keyPath, key.ExportPkcs8PrivateKeyPem());

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var exitCode = await CalloraCliApplication.RunAsync(
                ["plugin", "sign", "--plugin", tempDir, "--key", keyPath],
                stdout,
                stderr,
                tempDir,
                CancellationToken.None);

            Assert.Equal(1, exitCode);
            Assert.Contains("registry.json", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
