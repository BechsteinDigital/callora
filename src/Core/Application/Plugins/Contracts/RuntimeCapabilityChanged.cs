namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Payload of <see cref="IRuntimeCapabilitySource.CapabilitiesChanged"/>: one runtime-conditional
/// capability became satisfied or unsatisfied in a given scope.
/// </summary>
/// <param name="Capability">The capability code that changed.</param>
/// <param name="WorkspaceKey">The scope, or <see langword="null"/> for a global change.</param>
/// <param name="Satisfied"><see langword="true"/> when the capability is now provided; <see langword="false"/> when it is not.</param>
public sealed record RuntimeCapabilityChanged(string Capability, string? WorkspaceKey, bool Satisfied);
