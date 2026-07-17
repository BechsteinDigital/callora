using Callora.Core.Application.Integrations;
using Callora.Core.Domain.Integrations;
using System.Collections.Concurrent;

namespace Callora.Core.Application.Integrations;

/// <summary>
/// Thread-safe in-memory integration store for tests and hosts without a database.
/// </summary>
public sealed class InMemoryIntegrationCredentialStore : IIntegrationCredentialStore
{
    private readonly ConcurrentDictionary<Guid, IntegrationCredential> _byId = new();

    public Task<IntegrationCredential?> FindActiveByKeyHashAsync(
        string keyHash,
        CancellationToken cancellationToken = default)
    {
        var match = _byId.Values.FirstOrDefault(c =>
            c.IsActive &&
            c.RevokedAtUtc is null &&
            string.Equals(c.KeyHash, keyHash, StringComparison.Ordinal));
        return Task.FromResult(match);
    }

    public Task<IReadOnlyList<IntegrationCredential>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IntegrationCredential> all = _byId.Values
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Task.FromResult(all);
    }

    public Task<IntegrationCredential?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _byId.TryGetValue(id, out var credential);
        return Task.FromResult(credential);
    }

    public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var exists = _byId.Values.Any(c =>
            string.Equals(c.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task AddAsync(IntegrationCredential credential, CancellationToken cancellationToken = default)
    {
        _byId[credential.Id] = credential;
        return Task.CompletedTask;
    }

    public Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_byId.TryGetValue(id, out var credential) || !credential.IsActive)
        {
            return Task.FromResult(false);
        }

        credential.IsActive = false;
        credential.RevokedAtUtc = DateTimeOffset.UtcNow;
        return Task.FromResult(true);
    }
}
