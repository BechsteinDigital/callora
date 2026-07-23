namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// One runtime-conditional capability a plugin currently provides, in a given scope. A grant means the
/// plugin's runtime condition for <paramref name="Capability"/> holds right now (for example a healthy
/// voice channel exists). Absence of a grant means the capability is not currently provided.
/// </summary>
/// <param name="Capability">The capability code, e.g. <c>communication.voice</c>.</param>
/// <param name="WorkspaceKey">The workspace the grant applies to, or <see langword="null"/> for a global grant.</param>
public sealed record RuntimeCapabilityGrant(string Capability, string? WorkspaceKey);
