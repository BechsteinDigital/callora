using Callora.Plugin.Communication.Application.Accounts;

namespace Callora.Host.Backend.Tests.Plugins.Voip;

/// <summary>In-memory SIP account store for import tests.</summary>
internal sealed class InMemorySipAccountStore : ISipAccountStore
{
    private readonly Dictionary<(string Workspace, string Id), SipAccountEntry> _entries = new();

    public int CreateCount { get; private set; }

    public Task<IReadOnlyList<SipAccountEntry>> ListAsync(string workspaceKey, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SipAccountEntry> result = _entries
            .Where(x => x.Key.Workspace == workspaceKey)
            .Select(x => x.Value)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<SipAccountEntry?> GetAsync(string workspaceKey, string sipAccountId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_entries.GetValueOrDefault((workspaceKey, sipAccountId)));

    public Task<SipAccountEntry> CreateAsync(string workspaceKey, UpsertSipAccountRequest request, CancellationToken cancellationToken = default)
    {
        CreateCount++;
        var id = SipAccountIdFactory.Create(request.Username, request.Domain);
        var now = DateTimeOffset.UtcNow;
        var entry = new SipAccountEntry(id, request.Username, request.Domain, request.DisplayName, request.Secret, request.IsActive, now, now);
        _entries[(workspaceKey, id)] = entry;
        return Task.FromResult(entry);
    }

    public Task<SipAccountEntry?> UpdateAsync(string workspaceKey, string sipAccountId, UpsertSipAccountRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> DeleteAsync(string workspaceKey, string sipAccountId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_entries.Remove((workspaceKey, sipAccountId)));

    public Task<IReadOnlyList<string>> ListWorkspaceKeysAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> keys = _entries.Keys.Select(x => x.Workspace).Distinct().ToArray();
        return Task.FromResult(keys);
    }

    public void Seed(string workspaceKey, SipAccountEntry entry) => _entries[(workspaceKey, entry.SipAccountId)] = entry;
}
