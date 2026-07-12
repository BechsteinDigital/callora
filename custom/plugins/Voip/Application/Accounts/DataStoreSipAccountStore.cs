using System.Text.Json;
using Callora.Host.PluginContracts.Application.Data;

namespace Callora.Plugins.Voip.Application.Accounts;

/// <summary>
/// SIP account store backed by the host-provided plugin data store.
/// </summary>
public sealed class DataStoreSipAccountStore(IPluginDataStore dataStore) : ISipAccountStore
{
    private const string Collection = "sip-accounts";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<SipAccountEntry>> ListAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        var entries = await dataStore
            .ListAsync(new PluginDataCollectionKey(VoipPlugin.Id, workspaceKey, Collection), cancellationToken)
            .ConfigureAwait(false);

        return entries
            .Select(entry => Deserialize(entry.JsonDocument))
            .Where(static entry => entry is not null)
            .Select(static entry => entry!)
            .OrderBy(static entry => entry.SipAccountId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<SipAccountEntry?> GetAsync(
        string workspaceKey,
        string sipAccountId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sipAccountId))
            return null;

        var json = await dataStore
            .GetAsync(BuildKey(workspaceKey, sipAccountId.Trim()), cancellationToken)
            .ConfigureAwait(false);

        return json is null ? null : Deserialize(json);
    }

    public async Task<SipAccountEntry> CreateAsync(
        string workspaceKey,
        UpsertSipAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = DateTimeOffset.UtcNow;
        var id = SipAccountIdFactory.Create(request.Username, request.Domain);

        var existing = await GetAsync(workspaceKey, id, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new InvalidOperationException($"SIP account '{id}' already exists.");
        }

        var entry = new SipAccountEntry(
            SipAccountId: id,
            Username: request.Username.Trim(),
            Domain: request.Domain.Trim(),
            DisplayName: request.DisplayName.Trim(),
            Secret: request.Secret,
            IsActive: request.IsActive,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);

        await SaveAsync(workspaceKey, entry, cancellationToken).ConfigureAwait(false);
        return entry;
    }

    public async Task<SipAccountEntry?> UpdateAsync(
        string workspaceKey,
        string sipAccountId,
        UpsertSipAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var current = await GetAsync(workspaceKey, sipAccountId, cancellationToken).ConfigureAwait(false);
        if (current is null)
            return null;

        var updated = current with
        {
            Username = request.Username.Trim(),
            Domain = request.Domain.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Secret = request.Secret,
            IsActive = request.IsActive,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await SaveAsync(workspaceKey, updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public Task<bool> DeleteAsync(
        string workspaceKey,
        string sipAccountId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sipAccountId))
            return Task.FromResult(false);

        return dataStore.RemoveAsync(BuildKey(workspaceKey, sipAccountId.Trim()), cancellationToken);
    }

    public Task<IReadOnlyList<string>> ListWorkspaceKeysAsync(CancellationToken cancellationToken = default) =>
        dataStore.ListWorkspaceKeysAsync(VoipPlugin.Id, Collection, cancellationToken);

    private Task SaveAsync(string workspaceKey, SipAccountEntry entry, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(entry, JsonOptions);
        return dataStore.SetAsync(BuildKey(workspaceKey, entry.SipAccountId), json, cancellationToken);
    }

    private static PluginDataKey BuildKey(string workspaceKey, string sipAccountId) =>
        new(VoipPlugin.Id, workspaceKey, Collection, sipAccountId);

    private static SipAccountEntry? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SipAccountEntry>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
