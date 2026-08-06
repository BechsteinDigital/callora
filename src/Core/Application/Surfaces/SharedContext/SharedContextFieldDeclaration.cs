namespace Callora.Core.Application.Surfaces.SharedContext;

/// <summary>
/// One field of a shared context value and how far it travels.
/// </summary>
/// <param name="Name">Property name as it appears in the published value (camelCase, as on the wire).</param>
/// <param name="Visibility">Who receives it. Owner-only unless stated otherwise.</param>
/// <param name="Description">
/// What this field is, in plain words. Not decoration: a shared context field is personal data,
/// and the purpose it serves has to be nameable — the same documentation duty CAL0003 enforces
/// on the C# side.
/// </param>
public sealed record SharedContextFieldDeclaration(
    string Name,
    SharedContextVisibility Visibility = SharedContextVisibility.Owner,
    string? Description = null);
