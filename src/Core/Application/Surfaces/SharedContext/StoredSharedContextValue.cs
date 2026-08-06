namespace Callora.Core.Application.Surfaces.SharedContext;

/// <summary>
/// A published value with the moment it stops being readable. Expiry rides with the value rather
/// than in a sweeper: one place decides, and a value nobody asks for costs a dictionary entry.
/// </summary>
internal readonly record struct StoredSharedContextValue(
    IReadOnlyDictionary<string, object?> Value,
    DateTimeOffset ExpiresAtUtc);
