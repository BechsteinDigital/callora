namespace Callora.Administration.Api;

/// <summary>
/// The ordered plugin ids whose admin UI bundles the shell may load for a workspace.
/// <para>
/// The shell used to load every admin bundle in the published manifest, so a plugin's
/// interface appeared in workspaces it was never assigned to. The chain is resolved on the
/// server — assignment, entitlement, capability and runtime health all fold into it — and the
/// shell loads nothing the server did not name.
/// </para>
/// </summary>
/// <param name="WorkspaceKey">Workspace the chain was resolved for.</param>
/// <param name="Chain">Plugin ids in load order; earlier bundles may be extended by later ones.</param>
public sealed record UiChainApiResponse(string WorkspaceKey, IReadOnlyList<string> Chain);
