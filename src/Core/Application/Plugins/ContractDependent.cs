namespace Callora.Core.Application.Plugins;

/// <summary>
/// One installed plugin that depends on a shared contract, and whether the version currently
/// pinned satisfies what it asked for (#125 block D).
/// </summary>
/// <param name="PluginId">The depending plugin.</param>
/// <param name="RequiredRange">The npm-style range it declared in its manifest.</param>
/// <param name="IsSatisfied">
/// Whether the pinned version falls inside that range. False is the interesting case: it names
/// exactly which plugin an operator would break by replacing the contract.
/// </param>
public sealed record ContractDependent(
    string PluginId,
    string RequiredRange,
    bool IsSatisfied);
