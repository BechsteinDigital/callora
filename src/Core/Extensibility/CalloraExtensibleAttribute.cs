namespace Callora.Core.Extensibility;

/// <summary>
/// Marks a public type or member as an official Callora extension point that plugins may
/// implement, derive from, or decorate (REV2 §7.1). Absence of this marker means a public
/// API is usable but is not a sanctioned extension surface.
/// </summary>
/// <remarks>
/// Marked surfaces are enforced by CAL0003: they must carry XML documentation so the
/// extension contract stays legible. A stricter "plugins may derive only from marked types"
/// rule remains a later stage; until then the marker records intent rather than blocking.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method |
    AttributeTargets.Property | AttributeTargets.Event,
    Inherited = false,
    AllowMultiple = false)]
public sealed class CalloraExtensibleAttribute : Attribute
{
    /// <summary>Marks the target as an extension point with no additional note.</summary>
    public CalloraExtensibleAttribute()
    {
    }

    /// <summary>Marks the target as an extension point and records guidance for implementers.</summary>
    /// <param name="note">Human-readable note on how the extension point is meant to be used.</param>
    public CalloraExtensibleAttribute(string note) => Note = note;

    /// <summary>Marks the target as an extension point of a specific mode, optionally with guidance.</summary>
    /// <param name="mode">What a plugin may do with this surface.</param>
    /// <param name="note">Optional guidance for implementers of this extension point.</param>
    public CalloraExtensibleAttribute(ExtensionPointMode mode, string? note = null)
    {
        Mode = mode;
        Note = note;
    }

    /// <summary>Optional guidance for implementers of this extension point.</summary>
    public string? Note { get; }

    /// <summary>
    /// What a plugin may do with this surface. Defaults to
    /// <see cref="ExtensionPointMode.Contributable"/> — the common case for a marked
    /// contract; decoratable or replaceable host services state their mode explicitly.
    /// </summary>
    public ExtensionPointMode Mode { get; }
}
