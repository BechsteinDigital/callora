using System.Collections.Concurrent;
using Callora.Host.Backend.Application.Abstractions.Security;
using Callora.Host.Backend.Domain.Security;
using Microsoft.AspNetCore.Identity;

namespace Callora.Host.Backend.Tests.Support;

internal sealed class InMemoryBackendUserStore : IBackendUserStore
{
    private readonly ConcurrentDictionary<string, BackendUser> _users = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _workspaceMembers = new(StringComparer.OrdinalIgnoreCase);
    private readonly IPasswordHasher<BackendUser> _passwordHasher = new PasswordHasher<BackendUser>();

    public Task<BackendUser?> AuthenticateAsync(
        string login,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult<BackendUser?>(null);
        }

        var normalizedLogin = login.Trim();
        var user = _users.Values.SingleOrDefault(x =>
            string.Equals(x.ExternalId, normalizedLogin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Email, normalizedLogin, StringComparison.OrdinalIgnoreCase));
        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return Task.FromResult<BackendUser?>(null);
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return Task.FromResult(verification == PasswordVerificationResult.Failed ? null : user);
    }

    public Task<bool> IsWorkspaceMemberAsync(
        string externalId,
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(
            _workspaceMembers.TryGetValue(workspaceKey.Trim(), out var members) &&
            members.ContainsKey(externalId.Trim()));
    }

    public Task<string?> GetWorkspaceRoleAsync(
        string externalId,
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Task.FromResult<string?>(null);
        }

        if (_workspaceMembers.TryGetValue(workspaceKey.Trim(), out var members) &&
            members.TryGetValue(externalId.Trim(), out var role))
        {
            return Task.FromResult<string?>(role);
        }

        return Task.FromResult<string?>(null);
    }

    public Task<IReadOnlyList<BackendUser>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<BackendUser>>(
            _users.Values
                .OrderBy(x => x.ExternalId, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    public Task<IReadOnlyList<BackendUser>> ListByWorkspaceAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(workspaceKey) ||
            !_workspaceMembers.TryGetValue(workspaceKey.Trim(), out var members))
        {
            return Task.FromResult<IReadOnlyList<BackendUser>>([]);
        }

        var users = members.Keys
            .Select(externalId => _users.TryGetValue(externalId, out var user) ? user : null)
            .Where(user => user is not null)
            .Select(user => user!)
            .OrderBy(x => x.ExternalId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<BackendUser>>(users);
    }

    public Task<BackendUser?> GetByExternalIdAsync(
        string externalId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return Task.FromResult<BackendUser?>(null);
        }

        _users.TryGetValue(externalId.Trim(), out var user);
        return Task.FromResult(user);
    }

    public Task<BackendUser> UpsertCredentialsAsync(
        string externalId,
        string? email,
        string? displayName,
        string? password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        var normalizedExternalId = externalId.Trim();
        var nowUtc = DateTimeOffset.UtcNow;
        var existedBefore = _users.ContainsKey(normalizedExternalId);

        var user = _users.GetOrAdd(normalizedExternalId, _ => new BackendUser
        {
            Id = Guid.NewGuid(),
            ExternalId = normalizedExternalId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        });

        if (!existedBefore && string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Password is required when creating a new user.");
        }

        user.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        user.UpdatedAtUtc = nowUtc;

        if (!string.IsNullOrWhiteSpace(password))
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
            user.PasswordHashAlgorithm = "aspnet.identity.v3";
        }

        return Task.FromResult(user);
    }

    public Task<bool> RemoveAsync(
        string externalId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return Task.FromResult(false);
        }

        var removed = _users.TryRemove(externalId.Trim(), out _);
        foreach (var workspace in _workspaceMembers.Values)
        {
            workspace.TryRemove(externalId.Trim(), out _);
        }

        return Task.FromResult(removed);
    }

    public void AddWorkspaceMember(string workspaceKey, string externalId, string role = "member")
    {
        var members = _workspaceMembers.GetOrAdd(workspaceKey.Trim(), _ => new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        members[externalId.Trim()] = role.Trim();
    }
}
