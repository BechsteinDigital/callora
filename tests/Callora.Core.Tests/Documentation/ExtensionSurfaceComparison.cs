namespace Callora.Core.Tests.Documentation;

/// <summary>
/// Compares two extension-surface snapshots for the one breaking change that can be
/// recognised without judgement: removing a member that was already announced as deprecated.
/// </summary>
/// <remarks>
/// Lives apart from <see cref="TheExtensionSurfaceMatchesItsContractVersionTests"/> so the
/// rule can be exercised against synthetic snapshots. A rule that can only be tested by
/// actually breaking the real surface is a rule nobody tests.
/// </remarks>
internal static class ExtensionSurfaceComparison
{
    private const string ContractVersionMarker = "# contractVersion:";
    internal const string DeprecationMarker = "  # deprecated ";

    public static ExtensionSurfaceVerdict Compare(string baseline, string current)
    {
        var baselineSignatures = SignaturesOf(baseline);
        var currentSignatures = new HashSet<string>(SignaturesOf(current).Keys, StringComparer.Ordinal);

        // Keyed on the SIGNATURE, not the whole line: correcting an announced version is
        // bookkeeping, and a line-wise comparison would report it as a removal.
        var removedDeprecations = baselineSignatures
            .Where(entry => entry.Value && !currentSignatures.Contains(entry.Key))
            .Select(entry => entry.Key)
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToArray();

        var bumped = !string.Equals(
            ContractVersionOf(baseline),
            ContractVersionOf(current),
            StringComparison.Ordinal);

        return new ExtensionSurfaceVerdict(
            RequiresContractVersionBump: removedDeprecations.Length > 0 && !bumped,
            DeprecatedRemovals: removedDeprecations);
    }

    /// <summary>Signature → whether it carried a deprecation announcement.</summary>
    private static Dictionary<string, bool> SignaturesOf(string surface)
    {
        var signatures = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var line in surface.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var markerIndex = line.IndexOf(DeprecationMarker, StringComparison.Ordinal);
            var signature = markerIndex < 0 ? line : line[..markerIndex];
            signatures[signature] = markerIndex >= 0;
        }

        return signatures;
    }

    private static string ContractVersionOf(string surface) =>
        surface.ReplaceLineEndings("\n").Split('\n')
            .FirstOrDefault(line => line.StartsWith(ContractVersionMarker, StringComparison.Ordinal))
            ?.Substring(ContractVersionMarker.Length)
            .Trim()
        ?? string.Empty;
}
