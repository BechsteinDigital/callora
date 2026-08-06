namespace Callora.Core.Application.Surfaces.SharedContext;

/// <summary>
/// Cuts a published value down to what one subscriber may see (design §5.5 P1).
/// <para>
/// The whole defence rests here. Everything that reaches a browser is readable in that browser —
/// by DevTools, by the console, by every script on the page. So the question is never "who may
/// read this once it arrives" but "what arrives at all", and that is decided on this side.
/// </para>
/// </summary>
public static class SharedContextProjection
{
    /// <summary>
    /// The value as <paramref name="visibility"/> receives it: declared fields only, and of those
    /// only the ones that travel that far.
    /// </summary>
    /// <remarks>
    /// A field the publisher set but nobody declared is dropped. That direction is the safe one:
    /// a forgotten declaration costs a field nobody sees, while the reverse — publishing whatever
    /// happens to be on the object — is how records leak.
    /// </remarks>
    public static IReadOnlyDictionary<string, object?> Project(
        SharedContextKeyDeclaration declaration,
        IReadOnlyDictionary<string, object?> value,
        SharedContextVisibility visibility)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(value);

        var projected = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var field in declaration.FieldsFor(visibility))
        {
            if (value.TryGetValue(field.Name, out var fieldValue))
            {
                projected[field.Name] = fieldValue;
            }
        }

        return projected;
    }
}
