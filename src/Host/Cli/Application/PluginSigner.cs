using System.Security.Cryptography;
using System.Text.Json;
using Callora.Core.Application.Plugins.Signing;

namespace Callora.Host.Cli.Application;

/// <summary>
/// Signs a plugin directory: hashes the covered files (assembly + registry.json),
/// builds a signature manifest, signs it with an ECDSA P-256 private key (PEM), and
/// writes <c>plugin.signature.json</c>. Signing registry.json makes the plugin's
/// declared capabilities and entry type tamper-evident, not just the assembly.
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

        // Covered files: the loaded assembly and registry.json itself. AssemblyFileName
        // is guaranteed non-null by the guard above.
        var relativePaths = new[] { registry.AssemblyFileName, "registry.json" };
        var fileHashes = new List<PluginSignatureFileHash>();
        foreach (var relativePath in relativePaths)
        {
            string absolutePath;
            try
            {
                absolutePath = PluginContentHasher.ResolveContained(request.PluginDirectory, relativePath);
            }
            catch (ArgumentException exception)
            {
                return PluginSignResult.Fail(exception.Message);
            }

            if (!File.Exists(absolutePath))
            {
                return PluginSignResult.Fail($"Covered file not found: {relativePath}");
            }

            fileHashes.Add(new PluginSignatureFileHash(ToManifestPath(relativePath), PluginContentHasher.HashFile(absolutePath)));
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

    private static string ToManifestPath(string value) => value.Replace('\\', '/');
}
