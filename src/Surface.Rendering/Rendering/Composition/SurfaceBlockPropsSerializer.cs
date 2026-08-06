using System.Text.Json;
using Callora.Core.Application.Surfaces.Layout;

namespace Callora.Surface.Rendering.Rendering.Composition;

/// <summary>
/// Turns a block's bindings into the <c>data-callora-props</c> payload — and decides what does not
/// go in.
/// <para>
/// That attribute sits in the delivered HTML. Anyone who fetches the page reads it, on a Public
/// surface without signing in. So the filtering happens HERE, before markup exists, for the same
/// reason <c>SurfaceSlotResolver</c> filters views on the server rather than hiding them with CSS.
/// </para>
/// </summary>
public static class SurfaceBlockPropsSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The props for one block, or null when nothing survives — a block with no visible
    /// configuration should carry no attribute rather than an empty one.
    /// </summary>
    /// <param name="block">The placed block.</param>
    /// <param name="confidentialControls">
    /// Controls the block declared confidential. Their values never reach the attribute; the
    /// renderer resolves them where it is safe to and ships the result, not the input.
    /// </param>
    public static string? Serialize(
        SurfaceLayoutBlock block,
        IReadOnlySet<string>? confidentialControls = null)
    {
        ArgumentNullException.ThrowIfNull(block);

        var props = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, binding) in block.Config)
        {
            if (confidentialControls?.Contains(name) == true)
            {
                continue;
            }

            if (Value(binding) is { } value)
            {
                props[name] = value;
            }
        }

        return props.Count == 0 ? null : JsonSerializer.Serialize(props, Options);
    }

    private static object? Value(SurfaceBlockBinding binding) => binding.Source switch
    {
        // A literal the editor captured. It is already public by being on the page.
        SurfaceBlockBinding.StaticSource => binding.Value,

        // A context binding travels as a BINDING, never as a resolved value. Resolving it here
        // would put whatever the key currently holds into the page source — for every visitor,
        // regardless of who may see it. The browser resolves it against the channel, where the
        // projection has already decided what that visitor gets.
        SurfaceBlockBinding.ContextSource => new
        {
            __context = binding.Key,
            path = binding.Path,
        },

        // Nothing to serialise: the block falls back to its own default, and the section supplies
        // what is inherited.
        _ => null,
    };
}
