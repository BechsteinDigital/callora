using System.Collections.Generic;
using Callora.Core.Application.Plugins;
using Semver;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

/// <summary>
/// The install-time SemVer dependency gate (ABI compatibility). It enforces a plugin's
/// declared npm ranges against the versions the host provides: resolvable-but-out-of-range
/// rejects, resolvable-and-in-range and not-resolvable both pass, an unparseable range is a
/// hard error, and prerelease ordering follows SemVer (a prerelease never satisfies a plain
/// release lower bound). The provider is faked so no real assemblies are loaded.
/// </summary>
public sealed class PluginDependencyVersionGateTests
{
    private const string ContractId = "Callora.Plugin.Communication.Abstractions";

    [Fact]
    public void TryValidate_RangeSatisfied_IsValid()
    {
        var gate = NewGate((ContractId, "1.5.0"));

        var valid = gate.TryValidate(
            new Dictionary<string, string> { [ContractId] = ">=1.4.0" },
            out var error);

        Assert.True(valid);
        Assert.Null(error);
    }

    [Fact]
    public void TryValidate_PresentButOutOfRange_IsInvalid_AndNamesDetails()
    {
        var gate = NewGate((ContractId, "1.1.0"));

        var valid = gate.TryValidate(
            new Dictionary<string, string> { [ContractId] = ">=1.4.0" },
            out var error);

        Assert.False(valid);
        Assert.NotNull(error);
        Assert.Contains(ContractId, error);
        Assert.Contains(">=1.4.0", error);
        Assert.Contains("1.1.0", error);
    }

    [Fact]
    public void TryValidate_Unresolvable_IsValid_Skipped()
    {
        // Provider resolves nothing → dependency is skipped (presence is the planner's concern).
        var gate = new PluginDependencyVersionGate(new FakeProvidedContractVersionProvider());

        var valid = gate.TryValidate(
            new Dictionary<string, string> { [ContractId] = ">=1.4.0" },
            out var error);

        Assert.True(valid);
        Assert.Null(error);
    }

    [Fact]
    public void TryValidate_MultipleDependencies_OneViolated_NamesTheViolated()
    {
        var gate = NewGate(
            ("Callora.Core", "2.0.0"),
            (ContractId, "1.1.0"));

        var valid = gate.TryValidate(
            new Dictionary<string, string>
            {
                ["Callora.Core"] = ">=1.0.0",
                [ContractId] = ">=1.4.0"
            },
            out var error);

        Assert.False(valid);
        Assert.NotNull(error);
        Assert.Contains(ContractId, error);
        Assert.DoesNotContain("Callora.Core", error);
    }

    [Fact]
    public void TryValidate_PrereleaseWithinPrereleaseRange_IsValid()
    {
        var gate = NewGate((ContractId, "4.6.0-preview.3"));

        var valid = gate.TryValidate(
            new Dictionary<string, string> { [ContractId] = ">=4.6.0-preview.1" },
            out var error);

        Assert.True(valid);
        Assert.Null(error);
    }

    [Fact]
    public void TryValidate_PrereleaseAgainstReleaseLowerBound_IsInvalid()
    {
        // 4.6.0-preview.3 precedes the 4.6.0 release, so a plain ">=4.6.0" is not satisfied.
        var gate = NewGate((ContractId, "4.6.0-preview.3"));

        var valid = gate.TryValidate(
            new Dictionary<string, string> { [ContractId] = ">=4.6.0" },
            out var error);

        Assert.False(valid);
        Assert.NotNull(error);
        Assert.Contains(ContractId, error);
    }

    [Fact]
    public void TryValidate_InvalidRangeString_IsInvalid_NoThrow()
    {
        var gate = NewGate((ContractId, "1.5.0"));

        var valid = gate.TryValidate(
            new Dictionary<string, string> { [ContractId] = "not-a-range" },
            out var error);

        Assert.False(valid);
        Assert.NotNull(error);
        Assert.Contains(ContractId, error);
        Assert.Contains("not-a-range", error);
    }

    [Fact]
    public void TryValidate_EmptyDependencies_IsValid()
    {
        var gate = new PluginDependencyVersionGate(new FakeProvidedContractVersionProvider());

        Assert.True(gate.TryValidate(new Dictionary<string, string>(), out var error));
        Assert.Null(error);
        Assert.True(gate.TryValidate(null, out var nullError));
        Assert.Null(nullError);
    }

    private static PluginDependencyVersionGate NewGate(params (string ContractId, string Version)[] provided)
    {
        var provider = new FakeProvidedContractVersionProvider();
        foreach (var (id, version) in provided)
        {
            provider.Set(id, SemVersion.Parse(version, SemVersionStyles.Any));
        }

        return new PluginDependencyVersionGate(provider);
    }

    private sealed class FakeProvidedContractVersionProvider : IProvidedContractVersionProvider
    {
        private readonly Dictionary<string, SemVersion> _versions = new(System.StringComparer.OrdinalIgnoreCase);

        public void Set(string contractId, SemVersion version) => _versions[contractId] = version;

        public SemVersion? Resolve(string contractId) =>
            _versions.TryGetValue(contractId, out var version) ? version : null;
    }
}
