using Callora.Core.Application.Plugins;

namespace Callora.Core.Infrastructure.Plugins;

public sealed class LocalNuGetPackagePluginAssemblyResolver : INuGetPluginAssemblyResolver
{
    public ValueTask<NuGetPluginAssemblyResolveResult> ResolveAsync(
        string packageId,
        string packageVersion,
        string? assemblyFileName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return ValueTask.FromResult(NuGetPluginAssemblyResolveResult.Failure("packageId is required."));
        }

        if (string.IsNullOrWhiteSpace(packageVersion))
        {
            return ValueTask.FromResult(NuGetPluginAssemblyResolveResult.Failure("packageVersion is required."));
        }

        var packagesRoot = GetPackagesRoot();
        var packageRoot = Path.Combine(packagesRoot, packageId.ToLowerInvariant(), packageVersion.ToLowerInvariant());
        if (!Directory.Exists(packageRoot))
        {
            return ValueTask.FromResult(NuGetPluginAssemblyResolveResult.Failure(
                $"NuGet package '{packageId}/{packageVersion}' not found in local cache '{packagesRoot}'."));
        }

        var candidate = ResolveAssemblyCandidate(packageRoot, assemblyFileName);
        if (candidate is null)
        {
            return ValueTask.FromResult(NuGetPluginAssemblyResolveResult.Failure(
                assemblyFileName is null
                    ? $"No plugin assembly (*.dll) found in package '{packageId}/{packageVersion}'."
                    : $"Assembly '{assemblyFileName}' not found in package '{packageId}/{packageVersion}'."));
        }

        return ValueTask.FromResult(NuGetPluginAssemblyResolveResult.Success(candidate));
    }

    private static string GetPackagesRoot()
    {
        var overridePath = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".nuget", "packages");
    }

    private static string? ResolveAssemblyCandidate(string packageRoot, string? assemblyFileName)
    {
        if (!string.IsNullOrWhiteSpace(assemblyFileName))
        {
            return Directory
                .EnumerateFiles(packageRoot, assemblyFileName, SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        var preferredLibTfms = new[]
        {
            "net10.0",
            "net9.0",
            "net8.0"
        };

        foreach (var tfm in preferredLibTfms)
        {
            var libFolder = Path.Combine(packageRoot, "lib", tfm);
            if (!Directory.Exists(libFolder))
            {
                continue;
            }

            var match = Directory
                .EnumerateFiles(libFolder, "*.dll", SearchOption.TopDirectoryOnly)
                .Where(static path => !path.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (match is not null)
            {
                return match;
            }
        }

        return Directory
            .EnumerateFiles(packageRoot, "*.dll", SearchOption.AllDirectories)
            .Where(static path => !path.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }
}
