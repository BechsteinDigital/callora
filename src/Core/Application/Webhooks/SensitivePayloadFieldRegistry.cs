namespace Callora.Core.Application.Webhooks;

/// <summary>
/// The effective set of payload field names that webhook data-minimization masks
/// (PLAT-244). Combines a domain-neutral core baseline of generic person-related
/// fields with fields that plugins declare in their registry.json
/// <c>sensitiveFields</c> section. Thread-safe: plugins register on
/// install/update and clear on uninstall; the dispatcher reads the effective set
/// per delivery. A domain-neutral core carries no plugin-specific field names
/// (e.g. caller/callee numbers) — the Communication plugin declares those itself.
/// </summary>
public sealed class SensitivePayloadFieldRegistry
{
    // Generic, domain-neutral person-related fields the platform masks by default.
    private static readonly string[] CoreFields =
        ["target", "targetValue", "targetDisplayName", "displayName", "email"];

    private readonly object _sync = new();
    private readonly Dictionary<string, IReadOnlyCollection<string>> _pluginFields =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers (or replaces) the sensitive field names a plugin declares.</summary>
    public void RegisterPluginFields(string pluginId, IReadOnlyCollection<string> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(fields);

        var normalized = fields
            .Where(static f => !string.IsNullOrWhiteSpace(f))
            .Select(static f => f.Trim())
            .ToArray();

        lock (_sync)
        {
            _pluginFields[pluginId.Trim()] = normalized;
        }
    }

    /// <summary>Removes a plugin's declared fields (on uninstall).</summary>
    public void ClearPluginFields(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        lock (_sync)
        {
            _pluginFields.Remove(pluginId.Trim());
        }
    }

    /// <summary>The effective, case-insensitive set: core baseline plus all plugin-declared fields.</summary>
    public IReadOnlySet<string> EffectiveFields()
    {
        var set = new HashSet<string>(CoreFields, StringComparer.OrdinalIgnoreCase);
        lock (_sync)
        {
            foreach (var fields in _pluginFields.Values)
            {
                foreach (var field in fields)
                {
                    set.Add(field);
                }
            }
        }

        return set;
    }
}
