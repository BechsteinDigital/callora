namespace Callora.Core.Api;

/// <summary>
/// The scope to switch the current session to. Both keys absent means the platform scope, which
/// only an operator role resolves to — the same rule the login follows.
/// </summary>
/// <remarks>
/// <see cref="WorkspaceKey"/> wins when both are given, wie beim Anmelden: Wer einen Workspace
/// nennt, will darin arbeiten.
/// </remarks>
public sealed record SwitchScopeApiRequest(string? WorkspaceKey = null, string? TenantKey = null);
