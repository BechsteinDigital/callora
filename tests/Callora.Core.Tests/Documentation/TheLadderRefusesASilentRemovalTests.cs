using Callora.Core.Tests.Documentation;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// The one question about an extension-surface change that does not need judgement.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TheExtensionSurfaceMatchesItsContractVersionTests"/> deliberately refuses to
/// guess whether a change is breaking — a new method with a default implementation is not,
/// an extra parameter is, and a test that tries to tell them apart is eventually wrong and
/// then routed around. It asks the human.
/// </para>
/// <para>
/// Removing a member that was already marked deprecated is the exception. It is breaking by
/// definition — the deprecation said so — so no judgement is involved and the gate can
/// simply refuse it without a contract-version bump. That is what makes the middle rung
/// worth having: announcing a removal buys the announcement, and the announcement is then
/// enforced.
/// </para>
/// </remarks>
public sealed class TheLadderRefusesASilentRemovalTests
{
    private const string Signature = "System.Boolean Callora.Core.Fake.IThing.Old()";

    [Fact]
    public void RemovingADeprecatedMemberWithoutABumpIsRefused()
    {
        var verdict = ExtensionSurfaceComparison.Compare(
            baseline: Surface("v2", $"{Signature}  # deprecated since 0.9.2, error in v3"),
            current: Surface("v2"));

        Assert.True(verdict.RequiresContractVersionBump);
        Assert.Contains(Signature, verdict.DeprecatedRemovals);
    }

    [Fact]
    public void RemovingADeprecatedMemberWithABumpIsAllowed()
    {
        var verdict = ExtensionSurfaceComparison.Compare(
            baseline: Surface("v2", $"{Signature}  # deprecated since 0.9.2, error in v3"),
            current: Surface("v3"));

        Assert.False(verdict.RequiresContractVersionBump);
    }

    [Fact]
    public void MarkingAMemberDeprecatedIsAdditiveAndNeedsNoBump()
    {
        // The rung's whole promise: announcing costs nothing, so there is no reason to skip
        // straight to removal.
        var verdict = ExtensionSurfaceComparison.Compare(
            baseline: Surface("v2", Signature),
            current: Surface("v2", $"{Signature}  # deprecated since 0.9.2, error in v3"));

        Assert.False(verdict.RequiresContractVersionBump);
        Assert.Empty(verdict.DeprecatedRemovals);
    }

    [Fact]
    public void RemovingAMemberThatWasNeverDeprecatedStillGoesToTheHuman()
    {
        // Not silently allowed and not automatically refused — the existing gate still fails
        // on any difference and asks. This verdict only says "no automatic refusal applies".
        var verdict = ExtensionSurfaceComparison.Compare(
            baseline: Surface("v2", Signature),
            current: Surface("v2"));

        Assert.False(verdict.RequiresContractVersionBump);
        Assert.Empty(verdict.DeprecatedRemovals);
    }

    [Fact]
    public void ADeprecatedMemberThatOnlyChangesItsAnnouncementIsNotARemoval()
    {
        // Correcting the announced version is bookkeeping, not a removal — the signature is
        // still there. A comparison keyed on the whole line would call this a removal.
        var verdict = ExtensionSurfaceComparison.Compare(
            baseline: Surface("v2", $"{Signature}  # deprecated since 0.9.2, error in v3"),
            current: Surface("v2", $"{Signature}  # deprecated since 0.9.2, error in v4"));

        Assert.False(verdict.RequiresContractVersionBump);
        Assert.Empty(verdict.DeprecatedRemovals);
    }

    private static string Surface(string contractVersion, params string[] lines) =>
        string.Join('\n', ["# generated", $"# contractVersion: {contractVersion}", .. lines]) + "\n";
}
