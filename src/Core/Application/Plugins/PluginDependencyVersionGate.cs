using Callora.Core.Extensibility;
using Microsoft.Extensions.Logging;
using Semver;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Install-time gate that enforces a plugin's declared SemVer dependency ranges
/// against the versions the host actually provides (ABI compatibility). Only
/// <em>resolvable</em> dependencies are checked: a declared dependency the host
/// provides no assembly for is skipped here (presence/ordering is the activation
/// planner's job), but one it does provide must satisfy the declared npm range or
/// the install is rejected. An unparseable range is treated as a hard error.
/// </summary>
[CalloraInternal("Dependency-version enforcement gate — not a plugin contract")]
public sealed class PluginDependencyVersionGate(
    IProvidedContractVersionProvider versionProvider,
    ILogger<PluginDependencyVersionGate>? logger = null)
{
    private readonly IProvidedContractVersionProvider _versionProvider =
        versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));

    /// <summary>
    /// Validates every declared dependency. Returns <c>true</c> when all resolvable
    /// dependencies satisfy their ranges; otherwise <c>false</c> and sets
    /// <paramref name="error"/> to a message naming the offending dependency, its
    /// required range and the provided version.
    /// </summary>
    public bool TryValidate(IReadOnlyDictionary<string, string>? dependencies, out string? error)
    {
        error = null;
        if (dependencies is null || dependencies.Count == 0)
        {
            return true;
        }

        foreach (var (contractId, rangeText) in dependencies)
        {
            if (string.IsNullOrWhiteSpace(contractId))
            {
                continue;
            }

            if (!SemVersionRange.TryParseNpm(rangeText, out var range) || range is null)
            {
                error = $"Plugin dependency '{contractId}' declares an invalid version range '{rangeText}'.";
                logger?.LogWarning(
                    "Rejecting install: dependency {ContractId} has an unparseable range '{Range}'.",
                    contractId,
                    rangeText);
                return false;
            }

            var provided = _versionProvider.Resolve(contractId);
            if (provided is null)
            {
                // Not provided by the host — skip (presence is the planner's concern).
                logger?.LogDebug(
                    "Dependency {ContractId} is not host-provided; version gate skips it.",
                    contractId);
                continue;
            }

            if (!range.Contains(provided))
            {
                error =
                    $"Plugin dependency '{contractId}' requires '{rangeText}', " +
                    $"but the host provides {provided}.";
                logger?.LogWarning(
                    "Rejecting install: dependency {ContractId} requires {Range} but host provides {Provided}.",
                    contractId,
                    rangeText,
                    provided);
                return false;
            }
        }

        return true;
    }
}
