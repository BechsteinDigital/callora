using Callora.Host.Backend.Application.Configuration;
using Callora.Host.Backend.Domain.Configuration;
using Callora.Host.PluginContracts.Application.Secrets;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class EfSystemConfigStore(
    HostPersistenceDbContext dbContext,
    IPluginDataProtector dataProtector) : ISystemConfigStore
{
    private const string ProtectionScope = "callora-system-config";

    public async Task<IReadOnlyList<SystemConfigDefinitionSnapshot>> ListDefinitionsAsync(
        string? pluginId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.SystemConfigDefinitions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(pluginId))
        {
            var normalized = pluginId.Trim();
            query = query.Where(x => x.PluginId == normalized);
        }

        return await query
            .OrderBy(x => x.PluginId)
            .ThenBy(x => x.GroupName)
            .ThenBy(x => x.SortOrder)
            .Select(x => new SystemConfigDefinitionSnapshot(
                x.PluginId,
                x.Version,
                x.ConfigKey,
                x.Label,
                x.FieldType,
                x.Description,
                x.DefaultValueJson,
                x.GroupName,
                x.OptionsJson,
                x.SortOrder,
                x.IsActive))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ReplaceDefinitionsForPluginAsync(
        string pluginId,
        string version,
        IReadOnlyList<SystemConfigDefinitionInput> definitions,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        var normalized = pluginId.Trim();

        var existing = await dbContext.SystemConfigDefinitions
            .Where(x => x.PluginId == normalized)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        dbContext.SystemConfigDefinitions.RemoveRange(existing);

        var now = DateTimeOffset.UtcNow;
        foreach (var input in definitions)
        {
            dbContext.SystemConfigDefinitions.Add(new SystemConfigDefinition
            {
                Id = Guid.NewGuid(),
                PluginId = normalized,
                Version = version.Trim(),
                ConfigKey = input.ConfigKey.Trim(),
                Label = input.Label,
                FieldType = input.FieldType,
                Description = input.Description,
                DefaultValueJson = input.DefaultValueJson,
                GroupName = input.GroupName,
                OptionsJson = input.OptionsJson,
                SortOrder = input.SortOrder,
                IsActive = input.IsActive,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearDefinitionsForPluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        var normalized = pluginId.Trim();
        var existing = await dbContext.SystemConfigDefinitions
            .Where(x => x.PluginId == normalized)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        dbContext.SystemConfigDefinitions.RemoveRange(existing);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SystemConfigValueSnapshot>> ListValuesAsync(
        string pluginId,
        IReadOnlyList<(string Scope, string ScopeKey)> scopeChain,
        CancellationToken cancellationToken = default)
    {
        var normalized = pluginId.Trim();
        var all = await dbContext.SystemConfigValues
            .AsNoTracking()
            .Where(x => x.PluginId == normalized)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return all
            .Where(value => scopeChain.Any(scope =>
                value.Scope == scope.Scope &&
                string.Equals(value.ScopeKey, scope.ScopeKey, StringComparison.OrdinalIgnoreCase)))
            .Select(value => new SystemConfigValueSnapshot(
                value.PluginId,
                value.ConfigKey,
                value.Scope,
                value.ScopeKey,
                UnprotectValue(value.ValueJson),
                value.UpdatedAtUtc))
            .ToArray();
    }

    public async Task UpsertValuesAsync(
        string pluginId,
        string scope,
        string scopeKey,
        IReadOnlyDictionary<string, string?> valuesByKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        if (!SystemConfigScopes.IsValid(scope))
        {
            throw new ArgumentException($"Unknown config scope '{scope}'.", nameof(scope));
        }

        var normalizedPluginId = pluginId.Trim();
        var normalizedScopeKey = scopeKey?.Trim() ?? string.Empty;

        var existing = await dbContext.SystemConfigValues
            .Where(x => x.PluginId == normalizedPluginId && x.Scope == scope && x.ScopeKey == normalizedScopeKey)
            .ToDictionaryAsync(x => x.ConfigKey, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        var secretKeys = await ListSecretConfigKeysAsync(normalizedPluginId, cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        foreach (var (configKey, valueJson) in valuesByKey)
        {
            var normalizedKey = configKey.Trim();
            if (valueJson is null)
            {
                if (existing.TryGetValue(normalizedKey, out var toRemove))
                {
                    dbContext.SystemConfigValues.Remove(toRemove);
                }
                continue;
            }

            var storedValue = secretKeys.Contains(normalizedKey)
                ? dataProtector.Protect(ProtectionScope, valueJson)
                : valueJson;

            if (existing.TryGetValue(normalizedKey, out var current))
            {
                current.ValueJson = storedValue;
                current.UpdatedAtUtc = now;
            }
            else
            {
                dbContext.SystemConfigValues.Add(new SystemConfigValue
                {
                    Id = Guid.NewGuid(),
                    PluginId = normalizedPluginId,
                    ConfigKey = normalizedKey,
                    Scope = scope,
                    ScopeKey = normalizedScopeKey,
                    ValueJson = storedValue,
                    UpdatedAtUtc = now
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<HashSet<string>> ListSecretConfigKeysAsync(
        string pluginId,
        CancellationToken cancellationToken)
    {
        var definitions = await dbContext.SystemConfigDefinitions
            .AsNoTracking()
            .Where(x => x.PluginId == pluginId)
            .Select(x => new { x.ConfigKey, x.FieldType })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return definitions
            .Where(x => SystemConfigFieldTypes.IsSecret(x.FieldType))
            .Select(x => x.ConfigKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private string UnprotectValue(string storedValue) =>
        dataProtector.TryUnprotect(ProtectionScope, storedValue, out var plaintext)
            ? plaintext
            : storedValue; // Legacy-Klartext bleibt lesbar; Neuanlagen sind verschlüsselt.
}
