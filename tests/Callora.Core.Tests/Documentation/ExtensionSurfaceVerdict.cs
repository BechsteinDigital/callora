namespace Callora.Core.Tests.Documentation;

/// <summary>
/// What comparing two extension-surface snapshots established.
/// </summary>
/// <param name="RequiresContractVersionBump">
/// True only for the case that needs no judgement: a member that was announced as
/// deprecated has been removed while the contract version stayed put.
/// </param>
/// <param name="DeprecatedRemovals">The signatures that triggered it, for the message.</param>
internal sealed record ExtensionSurfaceVerdict(
    bool RequiresContractVersionBump,
    IReadOnlyList<string> DeprecatedRemovals);
