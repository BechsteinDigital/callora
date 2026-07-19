using System.Security.Cryptography;
using System.Text.Json;
using Callora.Core.Application.Plugins.Signing;

namespace Callora.Host.Cli.Application;

/// <summary>
/// Signs a plugin directory: hashes every file in it (except the signature file
/// itself), builds a signature manifest, signs it with an ECDSA P-256 private key
/// (PEM), and writes <c>plugin.signature.json</c>. Covering the whole directory —
/// dependent assemblies, UI bundles, templates, migrations, registry.json — makes
/// the entire package tamper-evident, so no content lives outside the signed set.
/// The signing key must not reside inside the plugin directory.
/// </summary>
internal sealed class PluginSigner
{
    private static readonly JsonSerializerOptions RegistryJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<PluginSignResult> SignAsync(PluginSignRequest request, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(request.PluginDirectory))
        {
            return PluginSignResult.Fail($"Plugin directory not found: {request.PluginDirectory}");
        }

        var registryPath = Path.Combine(request.PluginDirectory, "registry.json");
        if (!File.Exists(registryPath))
        {
            return PluginSignResult.Fail("registry.json was not found in the plugin directory.");
        }

        if (!File.Exists(request.KeyPath))
        {
            return PluginSignResult.Fail($"Signing key not found: {request.KeyPath}");
        }

        PluginRegistryManifest? registry;
        try
        {
            registry = JsonSerializer.Deserialize<PluginRegistryManifest>(
                await File.ReadAllTextAsync(registryPath, cancellationToken).ConfigureAwait(false),
                RegistryJsonOptions);
        }
        catch (JsonException exception)
        {
            return PluginSignResult.Fail($"registry.json could not be parsed: {exception.Message}");
        }

        if (registry is null ||
            string.IsNullOrWhiteSpace(registry.PluginId) ||
            string.IsNullOrWhiteSpace(registry.AssemblyFileName))
        {
            return PluginSignResult.Fail("registry.json is missing pluginId or assemblyFileName.");
        }

        // Covered files: every file in the plugin directory (except the signature file
        // itself). EnumeratePackageFiles is the shared definition of "package content"
        // the verifier also enforces, so the two never drift.
        var relativePaths = PluginContentHasher.EnumeratePackageFiles(request.PluginDirectory);

        // The declared entry assembly must actually be part of the package. AssemblyFileName
        // is guaranteed non-null by the guard above.
        var assemblyRelativePath = registry.AssemblyFileName!.Replace('\\', '/');
        if (!relativePaths.Contains(assemblyRelativePath, StringComparer.Ordinal))
        {
            return PluginSignResult.Fail($"Declared assembly '{registry.AssemblyFileName}' was not found in the plugin directory.");
        }

        var fileHashes = new List<PluginSignatureFileHash>(relativePaths.Count);
        foreach (var relativePath in relativePaths)
        {
            var absolutePath = PluginContentHasher.ResolveContained(request.PluginDirectory, relativePath);
            fileHashes.Add(new PluginSignatureFileHash(relativePath, PluginContentHasher.HashFile(absolutePath)));
        }

        using var key = ECDsa.Create();
        try
        {
            key.ImportFromPem(await File.ReadAllTextAsync(request.KeyPath, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            return PluginSignResult.Fail($"Could not load the signing key: {exception.Message}");
        }

        var unsigned = new PluginSignatureManifest(
            SchemaVersion: "1.0",
            PluginId: registry.PluginId!,
            Version: string.IsNullOrWhiteSpace(registry.Version) ? "0.0.0" : registry.Version!,
            Algorithm: PluginSignatureAlgorithms.EcdsaP256Sha256,
            SignerFingerprint: PluginSignatureCryptography.ComputeFingerprint(key),
            Files: fileHashes,
            Signature: null);

        var signature = PluginSignatureCryptography.Sign(
            PluginSignatureManifestSerializer.SerializeCanonical(unsigned),
            key);
        var signed = unsigned with { Signature = signature };

        var outputPath = request.OutputPath ?? Path.Combine(request.PluginDirectory, "plugin.signature.json");
        await File.WriteAllTextAsync(
                outputPath,
                PluginSignatureManifestSerializer.SerializeToFileJson(signed),
                cancellationToken)
            .ConfigureAwait(false);

        return PluginSignResult.Success(outputPath);
    }
}
