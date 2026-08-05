namespace Callora.Administration.Api;

/// <summary>
/// One plugin bound to a contract, and whether the pinned version satisfies what it asked for.
/// </summary>
/// <param name="PluginId">The depending plugin.</param>
/// <param name="RequiredRange">The npm-style range from its manifest.</param>
/// <param name="IsSatisfied">
/// False names exactly which plugin an operator would break by replacing the contract.
/// </param>
public sealed record ContractDependentApiResponse(
    string PluginId,
    string RequiredRange,
    bool IsSatisfied);
